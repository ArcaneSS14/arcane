// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared._Arcane.Reputation;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Arcane.Reputation;

public sealed class ReputationManager : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IServerNetManager _netMgr = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;

    private readonly Dictionary<NetUserId, ReputationInfo> _cacheReputation = new();
    private readonly Dictionary<NetUserId, DateTime> _playerConnectionTime = new();

    private ISawmill _sawmill = default!;
    private const string SawmillId = "reputation";

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill(SawmillId);

        _netMgr.RegisterNetMessage<ReputationNetMsg>();

        _netMgr.Connecting += OnConnecting;
        _netMgr.Connected += OnConnected;

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<UpdateCachedReputationEvent>(UpdateCachedReputation);
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnPlayerSpawn);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _netMgr.Connecting -= OnConnecting;
        _netMgr.Connected -= OnConnected;
    }

    #region Cache

    private void OnPlayerSpawn(PlayerBeforeSpawnEvent ev)
    {
        _playerConnectionTime[ev.Player.UserId] = DateTime.UtcNow;
    }

    private void OnConnected(object? sender, NetChannelArgs e)
    {
        _cacheReputation.TryGetValue(e.Channel.UserId, out var info);
        var msg = new ReputationNetMsg { Info = info };
        _netMgr.ServerSendMessage(msg, e.Channel);
    }

    private async Task OnConnecting(NetConnectingArgs e)
    {
        var value = await GetPlayerReputation(e.UserId);

        if (value == null)
            return;

        _cacheReputation[e.UserId] = new ReputationInfo { Value = value.Value };
    }

    private async void UpdateCachedReputation(UpdateCachedReputationEvent ev)
    {
        var player = ev.Player;
        if (!_cacheReputation.ContainsKey(player))
            return;

        var value = await GetPlayerReputation(player);

        if (value == null)
            return;

        _cacheReputation[player] = new ReputationInfo { Value = value.Value };
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        // Keep cached values for players who are still connected, drop the rest.
        var connectedPlayers = _netMgr.Channels.Select(channel => channel.UserId).ToHashSet();

        foreach (var userId in _cacheReputation.Keys.ToArray())
        {
            if (!connectedPlayers.Contains(userId))
                _cacheReputation.Remove(userId);
        }

        _playerConnectionTime.Clear();
    }

    #endregion

    #region PublicApi

    public async void SetPlayerReputation(NetUserId player, float value, string? admin = null)
    {
        var preValue = await GetPlayerReputation(player);
        if (preValue == null)
            return;

        await SetPlayerReputationTask(player.UserId, value);

        RaiseLocalEvent(new UpdateCachedReputationEvent(player));
        await LogReputationChange(player, preValue.Value, false, admin);
    }

    public async void ModifyPlayerReputation(NetUserId player, float value, string? admin = null)
    {
        var preValue = await GetPlayerReputation(player);
        if (preValue == null)
            return;

        await ModifyPlayerReputationTask(player.UserId, value);

        RaiseLocalEvent(new UpdateCachedReputationEvent(player));
        await LogReputationChange(player, preValue.Value, true, admin);
    }

    public async Task<float?> GetPlayerReputation(NetUserId player)
    {
        return await GetPlayerReputationTask(player.UserId);
    }

    /// <summary>
    /// Reads the cached value populated on connect. Prefer this on hot paths (like OOC chat)
    /// so we do not hit the database per message.
    /// </summary>
    public bool GetCachedPlayerReputation(NetUserId player, out float? value)
    {
        var success = _cacheReputation.TryGetValue(player, out var info);
        value = info?.Value;
        return success;
    }

    public bool GetCachedPlayerConnection(NetUserId player, out DateTime date)
    {
        var success = _playerConnectionTime.TryGetValue(player, out var dateTime);
        date = dateTime;
        return success;
    }

    /// <summary>
    /// Converts a reputation value into a weight used for weighted random player picks.
    /// </summary>
    public int GetPlayerWeight(float rep)
    {
        // Min-max return values
        const int minValue = 30;
        const int maxValue = 50;

        // Min-max reputation values
        const float minReputation = 0f;
        const float maxReputation = 1000f;

        if (rep < minReputation)
            return 20;

        var normalizedReputation = (rep - minReputation) / (maxReputation - minReputation);
        var result = (int) (minValue + normalizedReputation * (maxValue - minValue));

        return Math.Clamp(result, minValue, maxValue);
    }

    public ICommonSession PickPlayerBasedOnReputation(List<ICommonSession> prefList)
    {
        var list = new List<ICommonSession>();

        foreach (var session in prefList)
        {
            if (!GetCachedPlayerReputation(session.UserId, out var value) || value == null)
                continue;

            var weight = GetPlayerWeight(value.Value);

            for (var i = 0; i < weight; i++)
            {
                list.Add(session);
            }
        }

        return list.Count == 0 ? _random.Pick(prefList) : _random.Pick(list);
    }

    #endregion

    #region Private

    private async Task SetPlayerReputationTask(Guid player, float value)
    {
        try
        {
            await _db.SetPlayerReputation(player, value);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to set reputation for {player}: {e}");
        }
    }

    private async Task ModifyPlayerReputationTask(Guid player, float value)
    {
        try
        {
            await _db.ModifyPlayerReputation(player, value);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to modify reputation for {player}: {e}");
        }
    }

    private async Task<float?> GetPlayerReputationTask(Guid player)
    {
        try
        {
            return await _db.GetPlayerReputation(player);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to read reputation for {player}: {e}");
            return null;
        }
    }

    private async Task LogReputationChange(NetUserId user, float preValue, bool modify, string? admin = null)
    {
        var located = await _locator.LookupIdAsync(user);
        if (located == null)
            return;

        var newValue = await GetPlayerReputation(user);
        if (newValue == null)
            return;

        var adminName = admin != null ? $" by {admin}" : "";

        var msg = modify
            ? $"Reputation of {located.Username} was modified from {preValue} to {newValue.Value}{adminName}."
            : $"Reputation of {located.Username} was set from {preValue} to {newValue.Value}{adminName}.";

        _sawmill.Info(msg);
    }

    #endregion
}
