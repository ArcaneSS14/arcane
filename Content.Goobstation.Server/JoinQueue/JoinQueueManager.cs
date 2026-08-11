using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Connection;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.CCVar;
using Content.Shared._Arcane.JoinQueue;
using Content.Goobstation.Shared.JoinQueue;
using Prometheus;
using Robust.Server.Player;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Goobstation.Common.CCVar;
using Content.Server._RMC14.LinkAccount;
using Content.Server.Database;
using Content.Goobstation.Common.JoinQueue;
using Content.Goobstation.Server._Arcane.JoinQueue;

namespace Content.Goobstation.Server.JoinQueue;

// Весь этот файл был переписан Arcane. При мердж конфликтах рекомендуется просто оставлять старую реализацию.

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
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    private readonly JoinQueueState<ICommonSession> _queue = new();
    private readonly JoinQueueLimitBypassState<ICommonSession> _limitBypasses = new();
    // Arcane-edit-start
    private readonly Dictionary<NetUserId, ConnectedSessionRecord> _connectedSessions = new();
    private readonly Dictionary<NetUserId, ICommonSession> _pendingAdmissions = new();
    private readonly Dictionary<NetUserId, Dictionary<QueueMiniGameKind, MiniGameScoreState>> _miniGameScores = new();
    private readonly Dictionary<NetUserId, string> _miniGamePlayerNames = new();
    private readonly Dictionary<NetUserId, QueueWaitRecord> _queueWaitRecords = new();
    private long _nextQueueOrder;
    private int _queueWaitRecordOrder;
    // Arcane-edit-end

    /// <summary>
    ///     Rolling window of recent wait times in seconds for estimating queue wait.
    /// </summary>
    private readonly Queue<double> _recentWaitTimes = new();
    private const int MaxWaitTimeSamples = 20;
    private const int MaxQueueWaitLeaderboardEntries = 100;
    private const int QueueWaitHistoryPruneThreshold = 200;

    /// <summary>
    ///     Holds queue tickets for players who disconnected, allowing them to reclaim their order if they reconnect within the grace period.
    /// </summary>
    private readonly Dictionary<NetUserId, QueueReservation> _reservations = new();

    private bool _isEnabled;

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

    public int PlayerInQueueCount => _queue.Count;
    public int ActualPlayersCount => _player.PlayerCount - PlayerInQueueCount;

    public bool IsQueued(NetUserId userId)
    {
        return _queue.Contains(userId);
    }

    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("join-queue");
        _net.RegisterNetMessage<QueueUpdateMessage>();
        _net.RegisterNetMessage<QueueMiniGameScoreMessage>(OnMiniGameScore); // Arcane-edit

        _configuration.OnValueChanged(GoobCVars.QueueEnabled, OnQueueCVarChanged, true);
        _configuration.OnValueChanged(GoobCVars.PatreonSkip, OnPatreonCVarChanged, true);
        _configuration.OnValueChanged(CCVars.SoftMaxPlayers, OnPlayerLimitCVarChanged);
        _configuration.OnValueChanged(CCVars.AdminsCountForMaxPlayers, OnAdminCountCVarChanged);
        _adminManager.OnPermsChanged += OnAdminPermsChanged;
        _player.PlayerStatusChanged += OnPlayerStatusChanged;
        _userDb.AddOnFinishLoad(OnPlayerDataLoaded);
    }

    public void Update(float frameTime)
    {
        if (!_isEnabled)
            return;

        _infoRefreshTimer += frameTime;

        if (PlayerInQueueCount > 0 && _miniGameLeaderboardDirty)
        {
            _miniGameScoreBroadcastTimer += frameTime;
            if (_miniGameScoreBroadcastTimer >= MiniGameScoreUpdateIntervalSeconds)
            {
                _miniGameLeaderboardDirty = false;
                _miniGameScoreBroadcastTimer = 0f;
                if (_infoRefreshTimer >= InfoRefreshIntervalSeconds)
                {
                    _infoRefreshTimer = 0f;
                    CleanupExpiredReservations();
                }
                SendUpdateMessages();
                return;
            }
        }

        if (_infoRefreshTimer < InfoRefreshIntervalSeconds)
            return;

        _infoRefreshTimer = 0f;
        CleanupExpiredReservations();

        if (PlayerInQueueCount == 0)
            return;

        _miniGameLeaderboardDirty = false;
        SendUpdateMessages();
    }


    private void OnQueueCVarChanged(bool value)
    {
        if (_isEnabled == value)
            return;

        _isEnabled = value;

        if (value)
        {
            ProcessQueue();
            return;
        }

        var queuedSessions = _queue.Entries.Select(static entry => entry.Session).ToArray();
        var queuedUserIds = _queue.Entries.Select(static entry => entry.UserId).ToHashSet();
        _queue.Clear();
        _reservations.Clear();
        QueueCount.Set(0);

        foreach (var session in queuedSessions)
        {
            ClearMiniGameState(session.UserId);
            if (IsCurrentConnectedSession(session))
                session.Channel.Disconnect(Loc.GetString("queue-disabled-disconnect-reason"));
        }

        foreach (var record in _connectedSessions.Values.ToArray())
        {
            if (!queuedUserIds.Contains(record.Session.UserId))
                TrySendToGame(record.Session);
        }
    }

    private void OnPatreonCVarChanged(bool value)
    {
        if (_queue.PriorityEnabled == value)
            return;

        _queue.SetPriorityEnabled(value);

        if (_isEnabled)
            ProcessQueue();
    }

    private void OnPlayerLimitCVarChanged(int _)
    {
        if (_isEnabled)
            ProcessQueue();
    }

    private void OnAdminCountCVarChanged(bool _)
    {
        if (_isEnabled)
            ProcessQueue();
    }

    private void OnAdminPermsChanged(AdminPermsChangedEventArgs _)
    {
        if (_isEnabled &&
            PlayerInQueueCount > 0 &&
            !_configuration.GetCVar(CCVars.AdminsCountForMaxPlayers))
        {
            ProcessQueue();
        }
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected)
        {
            var removedSessionState = false;
            if (_connectedSessions.TryGetValue(e.Session.UserId, out var connected) &&
                ReferenceEquals(connected.Session, e.Session))
            {
                _connectedSessions.Remove(e.Session.UserId);
                removedSessionState = true;
            }

            if (_pendingAdmissions.TryGetValue(e.Session.UserId, out var pending) &&
                ReferenceEquals(pending, e.Session))
            {
                _pendingAdmissions.Remove(e.Session.UserId);
            }

            _limitBypasses.Remove(e.Session.UserId, e.Session);

            if (_queue.TryGet(e.Session.UserId, out var queued) &&
                ReferenceEquals(queued.Session, e.Session) &&
                _queue.TryRemove(e.Session.UserId, out queued))
            {
                var now = _gameTiming.RealTime;
                StoreQueueReservation(queued, now);
                removedSessionState = true;
            }

            if (removedSessionState)
                ClearMiniGameState(e.Session.UserId);

            if (!_isEnabled)
                return;

            ProcessQueue();
        }
        else if (e.NewStatus == SessionStatus.Connected)
        {
            var removedStaleQueueEntry = false;
            var now = _gameTiming.RealTime;

            if (_queue.TryGet(e.Session.UserId, out var staleEntry) &&
                !ReferenceEquals(staleEntry.Session, e.Session) &&
                _queue.TryRemove(e.Session.UserId, out staleEntry))
            {
                StoreQueueReservation(staleEntry, now);
                ClearMiniGameState(e.Session.UserId);
                removedStaleQueueEntry = true;
            }

            if (_pendingAdmissions.TryGetValue(e.Session.UserId, out var staleAdmission) &&
                !ReferenceEquals(staleAdmission, e.Session))
            {
                _pendingAdmissions.Remove(e.Session.UserId);
            }

            _limitBypasses.Remove(e.Session.UserId);

            _connectedSessions[e.Session.UserId] = new ConnectedSessionRecord(
                e.Session,
                _nextQueueOrder++,
                now);

            if (!_isEnabled)
                TrySendToGame(e.Session);
            else if (removedStaleQueueEntry)
                ProcessQueue();
        }
        else if (e.NewStatus == SessionStatus.InGame)
        {
            var removedQueueEntry = false;
            if (_queue.TryGet(e.Session.UserId, out var queued) &&
                ReferenceEquals(queued.Session, e.Session) &&
                _queue.TryRemove(e.Session.UserId, out queued))
            {
                var now = _gameTiming.RealTime;
                var waitSeconds = GetQueueWaitSeconds(queued, now);
                UpdateQueueWaitRecord(queued, now);
                RecordWaitTime(waitSeconds);
                QueueTimings.WithLabels("Waited").Observe(waitSeconds);
                ClearMiniGameState(e.Session.UserId);
                removedQueueEntry = true;
            }

            if (_connectedSessions.TryGetValue(e.Session.UserId, out var connected) &&
                ReferenceEquals(connected.Session, e.Session))
            {
                _connectedSessions.Remove(e.Session.UserId);
            }

            if (_pendingAdmissions.TryGetValue(e.Session.UserId, out var pending) &&
                ReferenceEquals(pending, e.Session))
            {
                _pendingAdmissions.Remove(e.Session.UserId);
            }

            if (_isEnabled &&
                (removedQueueEntry ||
                 PlayerInQueueCount > 0 &&
                 !_configuration.GetCVar(CCVars.AdminsCountForMaxPlayers)))
            {
                ProcessQueue();
            }
        }
    }


    private async void OnPlayerDataLoaded(ICommonSession session)
    {
        if (!_isEnabled)
        {
            _taskManager.RunOnMainThread(() =>
            {
                if (_isEnabled)
                    OnPlayerDataLoaded(session);
                else
                    TrySendToGame(session);
            });
            return;
        }

        try
        {
            var isPrivileged = await _connection.HasPrivilegedJoin(session);
            _taskManager.RunOnMainThread(() => FinishPlayerDataLoaded(session, isPrivileged));
        }
        catch (Exception exception)
        {
            _sawmill.Error(
                "Failed to determine join privileges for {UserId}; treating the session as non-privileged. {Exception}",
                session.UserId,
                exception);
            _taskManager.RunOnMainThread(() => FinishPlayerDataLoaded(session, false));
        }
    }

    private void FinishPlayerDataLoaded(ICommonSession session, bool isPrivileged)
    {
        if (!IsCurrentConnectedSession(session))
            return;

        if (_pendingAdmissions.TryGetValue(session.UserId, out var pending) &&
            ReferenceEquals(pending, session))
        {
            return;
        }

        if (!_isEnabled)
        {
            TrySendToGame(session);
            return;
        }

        if (isPrivileged)
        {
            var softMax = Math.Max(0, _configuration.GetCVar(CCVars.SoftMaxPlayers));
            var needsBypass = GetCountedPlayerCount(session) >= softMax;
            _reservations.Remove(session.UserId);

            if (!TrySendToGame(session))
                return;

            if (needsBypass)
            {
                _limitBypasses.Add(session.UserId, session);
                QueueBypassCount.Inc();
            }

            ProcessQueue();
            return;
        }

        if (!_connectedSessions.TryGetValue(session.UserId, out var connected) ||
            !ReferenceEquals(connected.Session, session))
        {
            return;
        }

        var now = _gameTiming.RealTime;
        var order = connected.Order;
        var isPriority = _linkAccount.GetPatron(session)?.Tier != null;
        var accumulatedWaitSeconds = 0d;

        if (_reservations.Remove(session.UserId, out var reservation) &&
            IsReservationActive(reservation, now))
        {
            order = reservation.Order;
            isPriority = reservation.IsPriority;
            accumulatedWaitSeconds = reservation.AccumulatedWaitSeconds;
        }

        var entry = new JoinQueueState<ICommonSession>.Entry(
            session.UserId,
            session,
            order,
            isPriority,
            connected.ConnectedAt,
            accumulatedWaitSeconds);

        if (_queue.Enqueue(entry))
            ProcessQueue();
    }

    private void ProcessQueue() // Arcane-edit
    {
        PruneInvalidQueueEntries();
        CleanupExpiredReservations();

        var players = GetCountedPlayerCount();
        var softMax = Math.Max(0, _configuration.GetCVar(CCVars.SoftMaxPlayers));

        while (players < softMax && _queue.TryDequeue(out var entry))
        {
            if (!IsCurrentConnectedSession(entry.Session))
                continue;

            var now = _gameTiming.RealTime;
            var waitSeconds = GetQueueWaitSeconds(entry, now);
            UpdateQueueWaitRecord(entry, now);

            if (!TrySendToGame(entry.Session))
                continue;

            RecordWaitTime(waitSeconds);
            QueueTimings.WithLabels("Waited").Observe(waitSeconds);
            ClearMiniGameState(entry.UserId);

            if (CountsTowardsPlayerLimit(entry.Session))
                players++;
        }

        SendUpdateMessages();
        QueueCount.Set(PlayerInQueueCount);
    }

    private void PruneInvalidQueueEntries()
    {
        for (var i = _queue.Entries.Count - 1; i >= 0; i--)
        {
            var entry = _queue.Entries[i];
            if (IsCurrentConnectedSession(entry.Session))
                continue;

            _queue.TryRemove(entry.UserId, out _);
            ClearMiniGameState(entry.UserId);
        }
    }

    private int GetCountedPlayerCount(ICommonSession? excludedSession = null)
    {
        var players = ActualPlayersCount;

        if (excludedSession != null && !_queue.Contains(excludedSession.UserId))
        {
            players--;
        }

        var activeLimitBypasses = 0;
        foreach (var (userId, session) in _limitBypasses.Sessions)
        {
            if (ReferenceEquals(session, excludedSession) ||
                _queue.Contains(userId) ||
                !_player.TryGetSessionById(userId, out var current) ||
                !ReferenceEquals(current, session))
            {
                continue;
            }

            activeLimitBypasses++;
        }

        var exemptAdmins = 0;
        var overlappingExemptions = 0;
        if (!_configuration.GetCVar(CCVars.AdminsCountForMaxPlayers))
        {
            foreach (var admin in _adminManager.ActiveAdmins)
            {
                if (ReferenceEquals(admin, excludedSession) ||
                    _queue.Contains(admin.UserId) ||
                    !_player.TryGetSessionById(admin.UserId, out var current) ||
                    !ReferenceEquals(current, admin))
                {
                    continue;
                }

                exemptAdmins++;
                if (_limitBypasses.Contains(admin.UserId, admin))
                    overlappingExemptions++;
            }
        }

        return CalculateCountedPlayerCount(
            players,
            activeLimitBypasses,
            exemptAdmins,
            overlappingExemptions);
    }

    internal static int CalculateCountedPlayerCount(
        int actualPlayers,
        int limitBypasses,
        int exemptAdmins,
        int overlappingExemptions)
    {
        DebugTools.Assert(actualPlayers >= 0);
        DebugTools.Assert(limitBypasses >= 0);
        DebugTools.Assert(exemptAdmins >= 0);
        DebugTools.Assert(overlappingExemptions >= 0);
        DebugTools.Assert(overlappingExemptions <= limitBypasses);
        DebugTools.Assert(overlappingExemptions <= exemptAdmins);

        return Math.Max(0, actualPlayers - limitBypasses - exemptAdmins + overlappingExemptions);
    }

    private bool CountsTowardsPlayerLimit(ICommonSession session)
    {
        return _configuration.GetCVar(CCVars.AdminsCountForMaxPlayers) ||
               !_adminManager.IsAdmin(session);
    }

    private void RecordWaitTime(double waitSeconds)
    {
        _recentWaitTimes.Enqueue(waitSeconds);
        while (_recentWaitTimes.Count > MaxWaitTimeSamples)
            _recentWaitTimes.Dequeue();
    }

    private static float GetEstimatedWaitForPosition(int position, int total, double averageWaitSeconds)
    {
        if (averageWaitSeconds < 0d)
            return -1f;

        return (float) (averageWaitSeconds * ((double) position / Math.Max(total, 1)));
    }

    private void SendUpdateMessages()
    {
        var totalInQueue = _queue.Count;
        if (totalInQueue == 0)
            return;

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

        var now = _gameTiming.RealTime;
        foreach (var entry in _queue.Entries)
        {
            UpdateQueueWaitRecord(entry, now);
            playerNames.Add(entry.Session.Name);
            playerWaitSeconds.Add((float) GetQueueWaitSeconds(entry, now));
        }

        PruneQueueWaitRecords();
        var queueWaitLeaderboard = BuildQueueWaitLeaderboard();
        var queueWaitNames = new List<string>(queueWaitLeaderboard.Count);
        var queueWaitSeconds = new List<float>(queueWaitLeaderboard.Count);
        foreach (var entry in queueWaitLeaderboard)
        {
            queueWaitNames.Add(entry.Name);
            queueWaitSeconds.Add(entry.WaitSeconds);
        }
        // Arcane-edit-end

        var averageWaitSeconds = _recentWaitTimes.Count == 0 ? -1d : _recentWaitTimes.Average();

        for (var i = 0; i < _queue.Entries.Count; i++)
        {
            var entry = _queue.Entries[i];
            var position = i + 1;
            entry.Session.Channel.SendMessage(new QueueUpdateMessage
            {
                Total = totalInQueue,
                Position = position,
                IsPatron = _queue.PriorityEnabled && entry.IsPriority,
                EstimatedWaitSeconds = GetEstimatedWaitForPosition(position, totalInQueue, averageWaitSeconds),
                MapName = mapName,
                GameMode = gameMode,
                ServerPlayerCount = serverPlayerCount,
                MaxPlayerCount = maxPlayerCount,
                RoundDurationMinutes = roundDurationMinutes,
                YourName = entry.Session.Name,
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
        var maxScore = GetMaxMiniGameScore(message.Game);
        if (maxScore == 0 ||
            !_queue.TryGet(message.MsgChannel.UserId, out var queued) ||
            !ReferenceEquals(queued.Session.Channel, message.MsgChannel))
            return;

        var score = Math.Clamp(message.Score, 0, maxScore);
        var session = queued.Session;
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

    private void UpdateQueueWaitRecord(JoinQueueState<ICommonSession>.Entry entry, TimeSpan now)
    {
        var waitSeconds = (float) GetQueueWaitSeconds(entry, now);
        if (_queueWaitRecords.TryGetValue(entry.UserId, out var record))
        {
            _queueWaitRecords[entry.UserId] = record with
            {
                Name = entry.Session.Name,
                WaitSeconds = Math.Max(record.WaitSeconds, waitSeconds),
            };
            return;
        }

        _queueWaitRecords[entry.UserId] = new QueueWaitRecord(entry.Session.Name, waitSeconds, _queueWaitRecordOrder++);
    }

    private static double GetQueueWaitSeconds(JoinQueueState<ICommonSession>.Entry entry, TimeSpan now)
    {
        return entry.AccumulatedWaitSeconds + Math.Max(0d, (now - entry.WaitStartedAt).TotalSeconds);
    }

    private List<QueueWaitRecord> BuildQueueWaitLeaderboard()
    {
        return _queueWaitRecords.Values
            .OrderByDescending(static entry => entry.WaitSeconds)
            .ThenBy(static entry => entry.Order)
            .Take(MaxQueueWaitLeaderboardEntries)
            .ToList();
    }

    private void PruneQueueWaitRecords()
    {
        if (_queueWaitRecords.Count <= QueueWaitHistoryPruneThreshold + _queue.Count + _reservations.Count)
            return;

        var retained = _queueWaitRecords
            .OrderByDescending(static pair => pair.Value.WaitSeconds)
            .ThenBy(static pair => pair.Value.Order)
            .Take(MaxQueueWaitLeaderboardEntries)
            .Select(static pair => pair.Key)
            .ToHashSet();

        foreach (var entry in _queue.Entries)
            retained.Add(entry.UserId);
        foreach (var userId in _reservations.Keys)
            retained.Add(userId);

        foreach (var userId in _queueWaitRecords.Keys.ToArray())
        {
            if (!retained.Contains(userId))
                _queueWaitRecords.Remove(userId);
        }
    }
    // Arcane-edit-end

    private void CleanupExpiredReservations()
    {
        var graceSeconds = _configuration.GetCVar(GoobCVars.QueueReconnectGraceSeconds);
        if (graceSeconds <= 0)
        {
            _reservations.Clear();
            return;
        }

        var now = _gameTiming.RealTime;
        var expired = new List<NetUserId>();

        foreach (var (userId, reservation) in _reservations)
        {
            if (!IsReservationActive(reservation, now))
                expired.Add(userId);
        }

        foreach (var userId in expired)
            _reservations.Remove(userId);
    }

    private void StoreQueueReservation(JoinQueueState<ICommonSession>.Entry entry, TimeSpan now)
    {
        UpdateQueueWaitRecord(entry, now);
        var waitSeconds = GetQueueWaitSeconds(entry, now);
        var graceSeconds = _configuration.GetCVar(GoobCVars.QueueReconnectGraceSeconds);

        if (graceSeconds > 0)
        {
            _reservations[entry.UserId] = new QueueReservation(
                now,
                entry.Order,
                entry.IsPriority,
                waitSeconds);
        }
        else
        {
            _reservations.Remove(entry.UserId);
        }

        QueueTimings.WithLabels("Unwaited").Observe(waitSeconds);
    }

    private bool IsReservationActive(QueueReservation reservation, TimeSpan now)
    {
        var graceSeconds = _configuration.GetCVar(GoobCVars.QueueReconnectGraceSeconds);
        return graceSeconds > 0 && now - reservation.DisconnectedAt <= TimeSpan.FromSeconds(graceSeconds);
    }

    private bool TrySendToGame(ICommonSession session)
    {
        if (!IsCurrentConnectedSession(session))
            return false;

        if (_pendingAdmissions.TryGetValue(session.UserId, out var pending) &&
            ReferenceEquals(pending, session))
        {
            return false;
        }

        _reservations.Remove(session.UserId);
        _pendingAdmissions[session.UserId] = session;
        Timer.Spawn(0, () =>
        {
            if (!_pendingAdmissions.TryGetValue(session.UserId, out var current) ||
                !ReferenceEquals(current, session))
            {
                return;
            }

            _pendingAdmissions.Remove(session.UserId);
            if (!IsCurrentConnectedSession(session))
                return;

            _player.JoinGame(session);
        });
        return true;
    }

    private bool IsCurrentConnectedSession(ICommonSession session)
    {
        return session.Status == SessionStatus.Connected &&
               _player.TryGetSessionById(session.UserId, out var current) &&
               ReferenceEquals(current, session);
    }

    private void ClearMiniGameState(NetUserId userId)
    {
        _miniGameScores.Remove(userId);
        _miniGamePlayerNames.Remove(userId);
    }

    // Arcane-edit-start
    private sealed record QueueReservation(
        TimeSpan DisconnectedAt,
        long Order,
        bool IsPriority,
        double AccumulatedWaitSeconds);

    private sealed record ConnectedSessionRecord(ICommonSession Session, long Order, TimeSpan ConnectedAt);

    private readonly record struct MiniGameScoreState(int Score, TimeSpan LastUpdateTime);

    private readonly record struct QueueWaitRecord(string Name, float WaitSeconds, int Order);
    // Arcane-edit-end
}
