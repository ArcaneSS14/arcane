// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Robust.Server.Player;
using Robust.Shared.Asynchronous;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

// has to be in Content.Server to exist
namespace Content.Server._Goobstation.Antag
{
    public sealed class LastRolledAntagManager
    {
        [Dependency] private readonly IServerDbManager _db = default!;
        [Dependency] private readonly ITaskManager _task = default!;
        [Dependency] private readonly IPlayerManager _player = default!;

        // Arcane-Start
        private readonly object _cacheLock = new();
        private readonly Dictionary<NetUserId, TimeSpan> _lastRolled = new();
        private readonly Dictionary<NetUserId, object> _loading = new();
        private readonly Dictionary<NetUserId, Task> _pendingSaveTasks = new();
        private ISawmill _sawmill = default!;

        public void Initialize()
        {
            _sawmill = Logger.GetSawmill("last_antag");
            _player.PlayerStatusChanged += OnPlayerStatusChanged;
        }

        /// <summary>
        /// Saves last rolled values to the database before allowing the server to shutdown.
        /// </summary>
        public void Shutdown()
        {
            Task[] saves;
            lock (_cacheLock)
                saves = _pendingSaveTasks.Values.ToArray();

            _player.PlayerStatusChanged -= OnPlayerStatusChanged;
            try
            {
                _task.BlockWaitOnTask(Task.WhenAll(saves).WaitAsync(TimeSpan.FromSeconds(5)));
            }
            catch (TimeoutException)
            {
                _sawmill.Warning($"Timed out saving {saves.Length} last rolled antag values during shutdown.");
            }
        }

        private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
        {
            if (args.NewStatus == SessionStatus.Connected)
                EnsureLoaded(args.Session.UserId);
            else if (args.NewStatus == SessionStatus.Disconnected)
            {
                lock (_cacheLock)
                {
                    _lastRolled.Remove(args.Session.UserId);
                    _loading.Remove(args.Session.UserId);
                }
            }
        }

        /// <summary>
        /// Sets a player's last rolled antag time in memory immediately and persists it asynchronously.
        /// </summary>
        public TimeSpan SetLastRolled(NetUserId userId, TimeSpan to)
        {
            var isConnected = _player.TryGetSessionById(userId, out var session) &&
                session.Status != SessionStatus.Disconnected;
            TimeSpan oldTime;
            Task save;
            lock (_cacheLock)
            {
                oldTime = _lastRolled.GetValueOrDefault(userId);
                if (isConnected)
                    _lastRolled[userId] = to;
                var previous = _pendingSaveTasks.GetValueOrDefault(userId) ?? Task.CompletedTask;
                save = SaveAsync(userId, to, previous);
                _pendingSaveTasks[userId] = save;
            }

            _ = TrackSaveCompletionAsync(userId, save);
            _sawmill.Info($"Setting {userId} last rolled antag to {to} from {oldTime}");
            return oldTime;
        }

        /// <summary>
        /// Reads the prefetched value without waiting for the database. A cache miss uses zero
        /// until the asynchronous load finishes.
        /// </summary>
        public TimeSpan GetLastRolled(NetUserId userId)
        {
            lock (_cacheLock)
            {
                if (_lastRolled.TryGetValue(userId, out var time))
                    return time;
            }

            if (_player.TryGetSessionById(userId, out var session) &&
                session.Status != SessionStatus.Disconnected)
                EnsureLoaded(userId);
            return TimeSpan.Zero;
        }

        private void EnsureLoaded(NetUserId userId)
        {
            object load;
            Task pendingSave;
            lock (_cacheLock)
            {
                if (_lastRolled.ContainsKey(userId) || _loading.ContainsKey(userId))
                    return;

                _loading[userId] = load = new object();
                pendingSave = _pendingSaveTasks.GetValueOrDefault(userId) ?? Task.CompletedTask;
            }

            _ = LoadAsync(userId, load, pendingSave);
        }

        private async Task LoadAsync(NetUserId userId, object load, Task pendingSave)
        {
            try
            {
                await pendingSave;
                var time = await _db.GetLastRolledAntag(userId);
                lock (_cacheLock)
                {
                    if (_loading.TryGetValue(userId, out var current) && ReferenceEquals(current, load))
                        _lastRolled.TryAdd(userId, time); // A newer in-memory assignment wins over an old database read.
                }
            }
            catch (Exception e)
            {
                _sawmill.Warning($"Could not load last rolled antag time for {userId}: {e}");
                lock (_cacheLock)
                {
                    if (_loading.TryGetValue(userId, out var current) && ReferenceEquals(current, load))
                        _lastRolled.TryAdd(userId, TimeSpan.Zero);
                }
            }
            finally
            {
                lock (_cacheLock)
                {
                    if (_loading.TryGetValue(userId, out var current) && ReferenceEquals(current, load))
                        _loading.Remove(userId);
                }
            }
        }

        private async Task SaveAsync(NetUserId userId, TimeSpan time, Task previous)
        {
            try
            {
                await previous;
                if (!await _db.SetLastRolledAntag(userId, time))
                    _sawmill.Warning($"Could not save last rolled antag time for {userId}: player not found.");
            }
            catch (Exception e)
            {
                _sawmill.Warning($"Could not save last rolled antag time for {userId}: {e}");
            }
        }

        private async Task TrackSaveCompletionAsync(NetUserId userId, Task save)
        {
            try
            {
                await save;
            }
            finally
            {
                lock (_cacheLock)
                {
                    if (_pendingSaveTasks.TryGetValue(userId, out var pending) && pending == save)
                        _pendingSaveTasks.Remove(userId);
                }
            }
        }
        // Arcane-End
    }
}
