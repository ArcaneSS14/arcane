using System.Linq;
using Content.Server.Connection;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.CCVar;
using Content.Shared._Arcane.JoinQueue;
using Content.Goobstation.Shared.JoinQueue;
using Prometheus;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Goobstation.Common.CCVar;
using Content.Server._RMC14.LinkAccount;
using Content.Server.Database;
using Content.Goobstation.Common.JoinQueue;

namespace Content.Goobstation.Server.JoinQueue;

/// <summary>
///     Manages new player connections when the server is full and queues them up, granting access when a slot becomes free
/// </summary>
public sealed class JoinQueueManager : IJoinQueueManager
{
    private static readonly Gauge QueueCount = Metrics.CreateGauge(
        "join_queue_total_count",
        "Amount of players in queue.");

    private static readonly Counter QueueBypassCount = Metrics.CreateCounter(
        "join_queue_bypass_count",
        "Amount of players who bypassed queue by privileges.");

    private static readonly Histogram QueueTimings = Metrics.CreateHistogram(
        "join_queue_timings",
        "Timings of players in queue",
        new HistogramConfiguration()
        {
            LabelNames = new[] { "type" },
            Buckets = Histogram.ExponentialBuckets(1, 2, 14),
        });


    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IConnectionManager _connection = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly LinkAccountManager _linkAccount = default!;
    [Dependency] private readonly UserDbDataManager _userDb = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameMapManager _gameMapManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private readonly List<ICommonSession> _queue = new();
    private readonly List<ICommonSession> _patronQueue = new();
    // Arcane-edit-start
    private readonly Dictionary<NetUserId, ICommonSession> _queuedSessions = new();
    private readonly Dictionary<NetUserId, Dictionary<QueueMiniGameKind, MiniGameScoreState>> _miniGameScores = new();
    private readonly Dictionary<NetUserId, string> _miniGamePlayerNames = new();
    private readonly Dictionary<NetUserId, QueueWaitRecord> _queueWaitRecords = new();
    private readonly Dictionary<NetUserId, float> _queueWaitOffsets = new();
    private int _queueWaitRecordOrder;
    // Arcane-edit-end

    /// <summary>
    ///     Rolling window of recent wait times in seconds for estimating queue wait.
    /// </summary>
    private readonly Queue<double> _recentWaitTimes = new();
    private const int MaxWaitTimeSamples = 20;

    /// <summary>
    ///     Holds queue positions for players who disconnected, allowing them to reclaim their spot if they reconnect within the grace period.
    /// </summary>
    private readonly Dictionary<NetUserId, QueueReservation> _reservations = new();

    private bool _isEnabled;
    private bool _patreonIsEnabled = true;

    /// <summary>
    ///     Interval for queue info refreshes
    /// </summary>
    private const float InfoRefreshIntervalSeconds = 30f;

    // Arcane-edit-start
    private const float MiniGameScoreUpdateIntervalSeconds = 1f;
    private float _infoRefreshTimer;
    private float _miniGameScoreBroadcastTimer;
    private bool _miniGameLeaderboardDirty;
    // Arcane-edit-end

    public int PlayerInQueueCount => _queue.Count + _patronQueue.Count;
    public int ActualPlayersCount => _player.PlayerCount - PlayerInQueueCount;

    public bool IsQueued(NetUserId userId)
    {
        return _queuedSessions.ContainsKey(userId);
    }

    private readonly HashSet<NetUserId> _bypassUsers = new();

    public void Initialize()
    {
        _net.RegisterNetMessage<QueueUpdateMessage>();
        _net.RegisterNetMessage<QueueMiniGameScoreMessage>(OnMiniGameScore); // Arcane-edit

        _configuration.OnValueChanged(GoobCVars.QueueEnabled, OnQueueCVarChanged, true);
        _configuration.OnValueChanged(GoobCVars.PatreonSkip, OnPatronCvarChanged, true);
        _player.PlayerStatusChanged += OnPlayerStatusChanged;
        _userDb.AddOnFinishLoad(OnPlayerDataLoaded);
    }

    public void Update(float frameTime)
    {
        if (!_isEnabled || PlayerInQueueCount == 0)
            return;

        if (_miniGameLeaderboardDirty)
        {
            _miniGameScoreBroadcastTimer += frameTime;
            if (_miniGameScoreBroadcastTimer >= MiniGameScoreUpdateIntervalSeconds)
            {
                _miniGameLeaderboardDirty = false;
                _miniGameScoreBroadcastTimer = 0f;
                SendUpdateMessages();
                return;
            }
        }

        _infoRefreshTimer += frameTime;
        if (_infoRefreshTimer < InfoRefreshIntervalSeconds)
            return;

        _infoRefreshTimer = 0f;
        _miniGameLeaderboardDirty = false;
        SendUpdateMessages();
    }


    private void OnQueueCVarChanged(bool value)
    {
        _isEnabled = value;

        if (!value)
        {
            foreach (var session in _queue)
                session.Channel.Disconnect("Queue was disabled");
            foreach (var session in _patronQueue)
                session.Channel.Disconnect("Queue was disabled");
        }
    }

    private void OnPatronCvarChanged(bool value)
    {
        if (_patreonIsEnabled && !value && _patronQueue.Count > 0)
        {
            _queue.AddRange(_patronQueue);
            _queue.Sort(static (a, b) => a.ConnectedTime.CompareTo(b.ConnectedTime));
            _patronQueue.Clear();
            ProcessQueue();
        }
        _patreonIsEnabled = value;
    }


    private async void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected)
        {
            var oldPosition = _queue.IndexOf(e.Session);
            var wasInQueue = oldPosition >= 0;
            var oldPatronPosition = _patronQueue.IndexOf(e.Session);
            var wasInPatronQueue = oldPatronPosition >= 0;

            if (wasInQueue)
                _queue.RemoveAt(oldPosition);
            if (wasInPatronQueue)
                _patronQueue.RemoveAt(oldPatronPosition);

            if (wasInQueue || wasInPatronQueue)
            {
                _queuedSessions.Remove(e.Session.UserId);
                UpdateQueueWaitRecord(e.Session, DateTime.UtcNow); // Arcane-edit
            }

            _bypassUsers.Remove(e.Session.UserId); // Arcane-edit

            if (wasInQueue || wasInPatronQueue)
            {
                var graceSeconds = _configuration.GetCVar(GoobCVars.QueueReconnectGraceSeconds);
                if (graceSeconds > 0)
                {
                    var accumulatedWaitSeconds = (float) GetQueueWaitSeconds(e.Session, DateTime.UtcNow); // Arcane-edit
                    _reservations[e.Session.UserId] = new QueueReservation(
                        DateTime.UtcNow,
                        wasInPatronQueue ? oldPatronPosition : oldPosition,
                        wasInPatronQueue,
                        accumulatedWaitSeconds);
                }

                QueueTimings.WithLabels("Unwaited").Observe(GetQueueWaitSeconds(e.Session, DateTime.UtcNow)); // Arcane-edit
            }

            if (!wasInQueue && !wasInPatronQueue && e.OldStatus != SessionStatus.InGame) // Arcane-edit
                return;

            ProcessQueue(); // Arcane-edit
        }
        else if (e.NewStatus == SessionStatus.Connected)
        {
            if (!_isEnabled)
                SendToGame(e.Session);
        }
    }


    private async void OnPlayerDataLoaded(ICommonSession session)
    {
        if (!_isEnabled)
            return;

        var isPrivileged = await _connection.HasPrivilegedJoin(session.UserId);
        var currentOnline = _player.PlayerCount - 1 - _bypassUsers.Count;
        var haveFreeSlot = currentOnline < _configuration.GetCVar(CCVars.SoftMaxPlayers);

        if (isPrivileged || haveFreeSlot)
        {
            SendToGame(session);
            _reservations.Remove(session.UserId);

            if (isPrivileged && !haveFreeSlot)
            {
                _bypassUsers.Add(session.UserId);
                QueueBypassCount.Inc();
            }

            return;
        }

        if (_reservations.Remove(session.UserId, out var reservation))
        {
            var graceSeconds = _configuration.GetCVar(GoobCVars.QueueReconnectGraceSeconds);
            if ((DateTime.UtcNow - reservation.DisconnectTime).TotalSeconds <= graceSeconds)
            {
                if (reservation.IsPatron && !_patreonIsEnabled)
                {
                    InsertByConnectedTime(_queue, session); // Arcane-edit
                }
                else
                {
                    var queue = reservation.IsPatron ? _patronQueue : _queue;
                    queue.Insert(Math.Min(reservation.QueuePosition, queue.Count), session);
                }

                _queueWaitOffsets[session.UserId] = reservation.AccumulatedWaitSeconds;
                _queuedSessions[session.UserId] = session;
                ProcessQueue();
                return;
            }
        }

        _queueWaitOffsets.Remove(session.UserId); // Arcane-edit

        InsertByConnectedTime(_queue, session); // Arcane-edit
        _queuedSessions[session.UserId] = session;
        ProcessQueue();
    }

    // Arcane-edit-start
    private static void InsertByConnectedTime(List<ICommonSession> queue, ICommonSession session)
    {
        var index = queue.FindIndex(other => other.ConnectedTime > session.ConnectedTime);
        if (index < 0)
        {
            queue.Add(session);
            return;
        }

        queue.Insert(index, session);
    }
    // Arcane-edit-end

    private void ProcessQueue() // Arcane-edit
    {
        var players = ActualPlayersCount;
        var softMax = _configuration.GetCVar(CCVars.SoftMaxPlayers);

        while (players < softMax && (_patronQueue.Count > 0 || _queue.Count > 0)) // Arcane-edit
        {
            // Arcane-edit-start
            var processPatron = _patronQueue.Count > 0 && (_patreonIsEnabled || _queue.Count == 0);
            var queue = processPatron ? _patronQueue : _queue;
            var session = queue[0];
            queue.RemoveAt(0);
            _queuedSessions.Remove(session.UserId);
            // Arcane-edit-end
            UpdateQueueWaitRecord(session, DateTime.UtcNow); // Arcane-edit
            var waitSeconds = GetQueueWaitSeconds(session, DateTime.UtcNow); // Arcane-edit
            RecordWaitTime(session);
            SendToGame(session);
            QueueTimings.WithLabels("Waited").Observe(waitSeconds); // Arcane-edit
            players++;
        }

        CleanupExpiredReservations();
        SendUpdateMessages();
        QueueCount.Set(PlayerInQueueCount); // Arcane-edit
    }

    private void RecordWaitTime(ICommonSession session)
    {
        var waitSeconds = GetQueueWaitSeconds(session, DateTime.UtcNow); // Arcane-edit
        _recentWaitTimes.Enqueue(waitSeconds);
        while (_recentWaitTimes.Count > MaxWaitTimeSamples)
            _recentWaitTimes.Dequeue();
    }

    private float GetEstimatedWaitForPosition(int position)
    {
        if (_recentWaitTimes.Count == 0)
            return -1f;

        var avg = _recentWaitTimes.Average();
        return (float) (avg * ((double) position / Math.Max(PlayerInQueueCount, 1)));
    }

    private void SendUpdateMessages()
    {
        var totalInQueue = _patronQueue.Count + _queue.Count;
        var currentPosition = 1;

        var mapName = _gameMapManager.GetSelectedMap()?.MapName ?? "Unknown";
        var gameMode = "Unknown";
        var roundDurationMinutes = 0;

        if (_entityManager.System<GameTicker>() is { } ticker)
        {
            var preset = ticker.CurrentPreset ?? ticker.Preset;
            if (preset != null)
                gameMode = Loc.GetString(preset.ModeTitle);

            if (ticker.RunLevel >= GameRunLevel.InRound)
            {
                var elapsed = _gameTiming.CurTime - ticker.RoundStartTimeSpan;
                roundDurationMinutes = (int) elapsed.TotalMinutes;
            }
        }

        var serverPlayerCount = ActualPlayersCount;
        var maxPlayerCount = _configuration.GetCVar(CCVars.SoftMaxPlayers);
        // Arcane-edit-start
        var miniGameLeaderboard = BuildMiniGameLeaderboard();
        var playerNames = new List<string>(totalInQueue);
        var playerWaitSeconds = new List<float>(totalInQueue);

        var now = DateTime.UtcNow;
        foreach (var session in _patronQueue)
        {
            UpdateQueueWaitRecord(session, now);
            playerNames.Add(session.Name);
            playerWaitSeconds.Add((float) GetQueueWaitSeconds(session, now));
        }

        foreach (var session in _queue)
        {
            UpdateQueueWaitRecord(session, now);
            playerNames.Add(session.Name);
            playerWaitSeconds.Add((float) GetQueueWaitSeconds(session, now));
        }

        var queueWaitLeaderboard = BuildQueueWaitLeaderboard();
        var queueWaitNames = new List<string>(queueWaitLeaderboard.Count);
        var queueWaitSeconds = new List<float>(queueWaitLeaderboard.Count);
        foreach (var entry in queueWaitLeaderboard)
        {
            queueWaitNames.Add(entry.Name);
            queueWaitSeconds.Add(entry.WaitSeconds);
        }
        // Arcane-edit-end

        for (var i = 0; i < _patronQueue.Count; i++, currentPosition++)
        {
            _patronQueue[i].Channel.SendMessage(new QueueUpdateMessage
            {
                Total = totalInQueue,
                Position = currentPosition,
                IsPatron = true,
                EstimatedWaitSeconds = GetEstimatedWaitForPosition(currentPosition),
                MapName = mapName,
                GameMode = gameMode,
                ServerPlayerCount = serverPlayerCount,
                MaxPlayerCount = maxPlayerCount,
                RoundDurationMinutes = roundDurationMinutes,
                YourName = _patronQueue[i].Name,
                PlayerNames = playerNames,
                // Arcane-edit-start
                PlayerWaitSeconds = playerWaitSeconds,
                QueueWaitLeaderboardNames = queueWaitNames,
                QueueWaitLeaderboardSeconds = queueWaitSeconds,
                MiniGameLeaderboard = miniGameLeaderboard,
                // Arcane-edit-end
            });
        }

        for (var i = 0; i < _queue.Count; i++, currentPosition++)
        {
            _queue[i].Channel.SendMessage(new QueueUpdateMessage
            {
                Total = totalInQueue,
                Position = currentPosition,
                IsPatron = false,
                EstimatedWaitSeconds = GetEstimatedWaitForPosition(currentPosition),
                MapName = mapName,
                GameMode = gameMode,
                ServerPlayerCount = serverPlayerCount,
                MaxPlayerCount = maxPlayerCount,
                RoundDurationMinutes = roundDurationMinutes,
                YourName = _queue[i].Name,
                PlayerNames = playerNames,
                // Arcane-edit-start
                PlayerWaitSeconds = playerWaitSeconds,
                QueueWaitLeaderboardNames = queueWaitNames,
                QueueWaitLeaderboardSeconds = queueWaitSeconds,
                MiniGameLeaderboard = miniGameLeaderboard,
                // Arcane-edit-end
            });
        }
    }

    // Arcane-edit-start
    private void OnMiniGameScore(QueueMiniGameScoreMessage message)
    {
        if (!Enum.IsDefined(typeof(QueueMiniGameKind), message.Game) ||
            !_queuedSessions.TryGetValue(message.MsgChannel.UserId, out var session))
            return;

        var score = Math.Clamp(message.Score, 0, GetMaxMiniGameScore(message.Game));
        _miniGamePlayerNames[session.UserId] = session.Name;
        if (!_miniGameScores.TryGetValue(session.UserId, out var scores))
        {
            scores = new Dictionary<QueueMiniGameKind, MiniGameScoreState>();
            _miniGameScores[session.UserId] = scores;
        }

        var now = _gameTiming.CurTime;
        var oldScore = 0;
        if (scores.TryGetValue(message.Game, out var oldState))
        {
            if (now - oldState.LastUpdateTime < TimeSpan.FromSeconds(MiniGameScoreUpdateIntervalSeconds))
                return;
            oldScore = oldState.Score;
        }

        if (oldScore >= score)
        {
            scores[message.Game] = new MiniGameScoreState(oldScore, now);
            return;
        }

        scores[message.Game] = new MiniGameScoreState(score, now);
        if (!_miniGameLeaderboardDirty)
            _miniGameScoreBroadcastTimer = 0f;
        _miniGameLeaderboardDirty = true;
    }

    private List<QueueMiniGameLeaderboardEntry> BuildMiniGameLeaderboard()
    {
        var entries = new List<QueueMiniGameLeaderboardEntry>(15);
        foreach (var game in Enum.GetValues<QueueMiniGameKind>())
        {
            var candidates = new List<(string Name, int Score)>();
            foreach (var (userId, scores) in _miniGameScores)
            {
                if (!scores.TryGetValue(game, out var state) ||
                    state.Score <= 0 ||
                    !_miniGamePlayerNames.TryGetValue(userId, out var playerName))
                    continue;
                candidates.Add((playerName, state.Score));
            }

            candidates.Sort(static (a, b) => b.Score.CompareTo(a.Score));
            for (var i = 0; i < Math.Min(5, candidates.Count); i++)
                entries.Add(new QueueMiniGameLeaderboardEntry(game, candidates[i].Name, candidates[i].Score));
        }

        return entries;
    }

    private static int GetMaxMiniGameScore(QueueMiniGameKind game)
    {
        return game switch
        {
            QueueMiniGameKind.Gyruss => 5000,
            QueueMiniGameKind.GoGoShitcurity => 10000,
            QueueMiniGameKind.SpaceInvaders => 6000,
            _ => 0,
        };
    }

    private void UpdateQueueWaitRecord(ICommonSession session, DateTime now)
    {
        var waitSeconds = (float) GetQueueWaitSeconds(session, now);
        if (_queueWaitRecords.TryGetValue(session.UserId, out var record))
        {
            _queueWaitRecords[session.UserId] = record with
            {
                Name = session.Name,
                WaitSeconds = Math.Max(record.WaitSeconds, waitSeconds),
            };
            return;
        }

        _queueWaitRecords[session.UserId] = new QueueWaitRecord(session.Name, waitSeconds, _queueWaitRecordOrder++);
    }

    private double GetQueueWaitSeconds(ICommonSession session, DateTime now)
    {
        var waitSeconds = (now - session.ConnectedTime).TotalSeconds;
        return waitSeconds + _queueWaitOffsets.GetValueOrDefault(session.UserId);
    }

    private List<QueueWaitRecord> BuildQueueWaitLeaderboard()
    {
        return _queueWaitRecords.Values
            .OrderByDescending(static entry => entry.WaitSeconds)
            .ThenBy(static entry => entry.Order)
            .Take(100)
            .ToList();
    }
    // Arcane-edit-end

    private void CleanupExpiredReservations()
    {
        var graceSeconds = _configuration.GetCVar(GoobCVars.QueueReconnectGraceSeconds);
        var now = DateTime.UtcNow;
        var expired = new List<NetUserId>();

        foreach (var (userId, reservation) in _reservations)
        {
            if ((now - reservation.DisconnectTime).TotalSeconds > graceSeconds)
                expired.Add(userId);
        }

        foreach (var userId in expired)
        {
            _reservations.Remove(userId);
            _queueWaitOffsets.Remove(userId); // Arcane-edit
        }
    }

    private void SendToGame(ICommonSession session)
    {
        // Arcane-edit-start
        _queuedSessions.Remove(session.UserId);
        _queueWaitOffsets.Remove(session.UserId);
        // Arcane-edit-end
        Timer.Spawn(0, () => _player.JoinGame(session));
    }

    // Arcane-edit-start
    private sealed record QueueReservation(DateTime DisconnectTime, int QueuePosition, bool IsPatron, float AccumulatedWaitSeconds);

    private readonly record struct MiniGameScoreState(int Score, TimeSpan LastUpdateTime);

    private readonly record struct QueueWaitRecord(string Name, float WaitSeconds, int Order);
    // Arcane-edit-end
}
