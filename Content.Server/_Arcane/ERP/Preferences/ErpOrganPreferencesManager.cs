using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Shared._Arcane.ERP.Preferences;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Log;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.ERP.Preferences;

public sealed class ErpOrganPreferencesManager : IPostInjectInit
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly UserDbDataManager _userDb = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private ISawmill _log = default!;

    private readonly Dictionary<NetUserId, Dictionary<int, ErpOrganPreferences>> _cache = new();
    // (userId, slot) → timestamp of last DB write; throttles DB writes to once per 5 seconds per slot.
    private readonly Dictionary<(NetUserId UserId, int Slot), TimeSpan> _lastSave = new();
    private readonly Dictionary<(NetUserId UserId, int Slot), ErpOrganPreferences> _pendingSaves = new();
    private readonly Dictionary<(NetUserId UserId, int Slot), CancellationTokenSource> _pendingSaveTokens = new();
    private readonly object _saveLock = new();

    private static readonly JsonSerializerOptions JsonOpts = new() { IncludeFields = true };

    public void PostInject()
    {
        _log = _logManager.GetSawmill("erp.prefs");
        _net.RegisterNetMessage<MsgErpOrganPreferences>();
        _net.RegisterNetMessage<MsgUpdateErpOrganPreferences>(HandleUpdate);

        _userDb.AddOnLoadPlayer(OnPlayerConnected);
        _userDb.AddOnFinishLoad(OnFinishLoad);
        _userDb.AddOnPlayerDisconnect(OnPlayerDisconnected);
    }

    private async Task OnPlayerConnected(ICommonSession session, CancellationToken cancel)
    {
        var slots = new Dictionary<int, ErpOrganPreferences>();
        _cache[session.UserId] = slots;

        var maxSlots = _cfg.GetCVar(CCVars.GameMaxCharacterSlots);
        for (var slot = 0; slot < maxSlots; slot++)
        {
            cancel.ThrowIfCancellationRequested();
            var json = await _db.GetErpOrganPreferencesAsync(session.UserId, slot);
            // Species-aware normalize happens in OnFinishLoad after ServerPreferencesManager is ready.
            slots[slot] = ErpOrganPreferencesNormalizer.Normalize(Deserialize(json));
        }
    }

    private void OnFinishLoad(ICommonSession session)
    {
        if (!_cache.TryGetValue(session.UserId, out var slots))
            return;

        // Re-normalize with species now that ServerPreferencesManager has loaded all character data.
        var maxSlots = _cfg.GetCVar(CCVars.GameMaxCharacterSlots);
        for (var slot = 0; slot < maxSlots; slot++)
        {
            if (slots.TryGetValue(slot, out var prefs))
                slots[slot] = Normalize(prefs, session.UserId, slot);
            SendToClient(session, slot);
        }
    }

    private void OnPlayerDisconnected(ICommonSession session)
    {
        _cache.Remove(session.UserId);
        FlushPendingSaves(session.UserId);

        var maxSlots = _cfg.GetCVar(CCVars.GameMaxCharacterSlots);
        lock (_saveLock)
        {
            for (var i = 0; i < maxSlots; i++)
                _lastSave.Remove((session.UserId, i));
        }
    }

    public void SendToClient(ICommonSession session, int slot)
    {
        if (!_cache.TryGetValue(session.UserId, out var slots))
            return;

        var prefs = slots.TryGetValue(slot, out var p) ? p : ErpOrganPreferences.Default();
        var msg = new MsgErpOrganPreferences { Slot = slot, Preferences = prefs };
        _net.ServerSendMessage(msg, session.Channel);
    }

    public ErpOrganPreferences? GetCached(NetUserId userId, int slot)
    {
        if (_cache.TryGetValue(userId, out var slots) && slots.TryGetValue(slot, out var prefs))
            return prefs;
        return null;
    }

    private static readonly TimeSpan SaveThrottle = TimeSpan.FromSeconds(5);

    private void HandleUpdate(MsgUpdateErpOrganPreferences msg)
    {
        if (!_players.TryGetSessionByChannel(msg.MsgChannel, out var session))
            return;

        var maxSlots = _cfg.GetCVar(CCVars.GameMaxCharacterSlots);
        var slot = msg.Slot;
        if (slot < 0 || slot >= maxSlots)
            return;

        var prefs = Normalize(msg.Preferences, session.UserId, slot);

        if (!_cache.TryGetValue(session.UserId, out var slots))
            _cache[session.UserId] = slots = new Dictionary<int, ErpOrganPreferences>();

        slots[slot] = prefs;

        // Throttle DB writes to once per 5 s per slot to prevent spam.
        var key = (session.UserId, slot);
        var now = _timing.RealTime;
        lock (_saveLock)
        {
            if (_lastSave.TryGetValue(key, out var last) && now - last < SaveThrottle)
            {
                QueuePendingSave(key, prefs, SaveThrottle - (now - last));
                return;
            }

            CancelPendingSave(key);
            _lastSave[key] = now;
        }

        _ = SaveAsync(session.UserId, slot, prefs);
    }

    private void QueuePendingSave((NetUserId UserId, int Slot) key, ErpOrganPreferences prefs, TimeSpan delay)
    {
        _pendingSaves[key] = prefs;
        if (_pendingSaveTokens.ContainsKey(key))
            return;

        var token = new CancellationTokenSource();
        _pendingSaveTokens[key] = token;
        _ = SavePendingAfterDelay(key, delay, token);
    }

    private async Task SavePendingAfterDelay((NetUserId UserId, int Slot) key, TimeSpan delay, CancellationTokenSource token)
    {
        try
        {
            await Task.Delay(delay, token.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        ErpOrganPreferences? prefs;
        lock (_saveLock)
        {
            if (!_pendingSaveTokens.TryGetValue(key, out var current) || current != token)
                return;

            _pendingSaveTokens.Remove(key);
            _pendingSaves.Remove(key, out prefs);
            _lastSave[key] = _timing.RealTime;
        }

        token.Dispose();

        if (prefs != null)
            await SaveAsync(key.UserId, key.Slot, prefs);
    }

    private void FlushPendingSaves(NetUserId userId)
    {
        var toSave = new List<((NetUserId UserId, int Slot) Key, ErpOrganPreferences Prefs)>();

        lock (_saveLock)
        {
            foreach (var (key, prefs) in _pendingSaves)
            {
                if (key.UserId != userId)
                    continue;

                toSave.Add((key, prefs));
            }

            foreach (var (key, _) in toSave)
            {
                if (_pendingSaveTokens.Remove(key, out var token))
                {
                    token.Cancel();
                    token.Dispose();
                }

                _pendingSaves.Remove(key);
                _lastSave.Remove(key);
            }
        }

        foreach (var (key, prefs) in toSave)
            SaveAsync(key.UserId, key.Slot, prefs).GetAwaiter().GetResult();
    }

    private void CancelPendingSave((NetUserId UserId, int Slot) key)
    {
        if (_pendingSaveTokens.Remove(key, out var token))
        {
            token.Cancel();
            token.Dispose();
        }

        _pendingSaves.Remove(key);
    }

    private ErpOrganPreferences Normalize(ErpOrganPreferences? input, NetUserId userId, int slot)
    {
        var (species, sex) = GetSlotSpeciesAndSex(userId, slot);
        if (species == null)
            return ErpOrganPreferencesNormalizer.Normalize(input);

        var definitions = ErpOrganEditorDefinitions.GetForSpecies(species, sex, _prototype, _componentFactory);
        return ErpOrganPreferencesNormalizer.Normalize(input, definitions);
    }

    private (string? Species, Sex Sex) GetSlotSpeciesAndSex(NetUserId userId, int slot)
    {
        var prefs = _prefs.GetPreferencesOrNull(userId);
        if (prefs?.Characters.TryGetValue(slot, out var profile) == true &&
            profile is HumanoidCharacterProfile humanoid)
        {
            return (humanoid.Species, humanoid.Sex);
        }

        return (null, Sex.Male);
    }

    private async Task SaveAsync(NetUserId userId, int slot, ErpOrganPreferences prefs)
    {
        try
        {
            var json = Serialize(prefs);
            await _db.SaveErpOrganPreferencesAsync(userId, slot, json);
        }
        catch (Exception e)
        {
            _log.Error($"Failed to save ERP organ preferences for {userId} slot {slot}: {e}");
        }
    }

    private static string Serialize(ErpOrganPreferences prefs)
        => JsonSerializer.Serialize(prefs, JsonOpts);

    private static ErpOrganPreferences Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ErpOrganPreferences.Default();

        try
        {
            return JsonSerializer.Deserialize<ErpOrganPreferences>(json, JsonOpts)
                   ?? ErpOrganPreferences.Default();
        }
        catch
        {
            return ErpOrganPreferences.Default();
        }
    }
}
