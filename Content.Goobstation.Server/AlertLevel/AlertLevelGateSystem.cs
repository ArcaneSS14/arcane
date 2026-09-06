using System.Linq;
using Content.Goobstation.Shared.AlertLevel;
using Content.Goobstation.Shared.Shadowling;
using Content.Goobstation.Shared.Slasher;
using Content.Server.Access.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Communications;
using Content.Server.GameTicking.Rules;
using Content.Server.NukeOps;
using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared._White.Xenomorphs;
using Content.Server.AlertLevel;
using Content.Shared.Heretic.Prototypes;
using Content.Shared.NukeOps;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.AlertLevel;

/// <summary>
/// Controls whether the gated alert level is unlocked.
/// </summary>
public sealed class AlertLevelGateSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarDeclaredEvent>(OnWarDeclared, after: new[] { typeof(NukeopsRuleSystem) });
        SubscribeLocalEvent<EventHereticAscension>(OnHereticAscension);
        SubscribeLocalEvent<ShadowlingAscendEvent>(OnShadowlingAscend);
        SubscribeLocalEvent<SlasherAscendedEvent>(OnSlasherAscend);
        SubscribeLocalEvent<XenomorphsAnnouncedEvent>(OnXenomorphsAnnounced);
        SubscribeLocalEvent<AlertLevelSelectAttemptEvent>(OnAlertSelectAttempt);
        SubscribeLocalEvent<AlertLevelGateUnlockRequestEvent>(OnUnlockRequest);
        SubscribeLocalEvent<CommunicationsConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnWarDeclared(ref WarDeclaredEvent ev)
    {
        if (ev.Status == WarConditionStatus.WarReady)
            UnlockAlertLevelGate();
    }

    private void OnHereticAscension(EventHereticAscension args) =>
        UnlockAlertLevelGate();

    private void OnShadowlingAscend(ShadowlingAscendEvent ev) =>
        UnlockAlertLevelGate();

    private void OnSlasherAscend(SlasherAscendedEvent ev) =>
        UnlockAlertLevelGate();

    private void OnXenomorphsAnnounced(XenomorphsAnnouncedEvent ev) =>
        UnlockAlertLevelGate();

    private void OnUnlockRequest(ref AlertLevelGateUnlockRequestEvent ev)
    {
        ev.Unlocked = UnlockAlertLevelGate(ev.Station, ev.AnnounceToStation);
    }

    /// <summary>
    /// Unlocks the gated alert level, allowing it to be manually activated from a
    /// communications console. Called when a qualifying threat occurs.
    /// </summary>
    public void UnlockAlertLevelGate()
    {
        var query = EntityQueryEnumerator<AlertLevelComponent>();
        while (query.MoveNext(out var station, out _))
            UnlockAlertLevelGate(station, true);
    }

    /// <summary>
    /// Unlocks the gated alert level for one station.
    /// </summary>
    public bool UnlockAlertLevelGate(EntityUid station, bool announceToStation)
    {
        var gate = EnsureComp<AlertLevelGateComponent>(station);
        if (gate.Unlocked)
            return false;

        gate.Unlocked = true;
        ClearPending(gate);

        if (announceToStation)
        {
            _chat.DispatchStationAnnouncement(
                station,
                Loc.GetString("alert-level-gate-unlocked-announcement"),
                Loc.GetString("comms-console-announcement-title-centcom"),
                playDefaultSound: false,
                colorOverride: Color.Red);
        }

        if (TryComp<StationDataComponent>(station, out var stationData))
        {
            _audio.PlayGlobal(
                gate.UnlockSound,
                _station.GetInStation(stationData),
                true,
                gate.UnlockSound.Params);
        }

        var ev = new AlertLevelGateUnlockedEvent(station);
        RaiseLocalEvent(ref ev);
        return true;
    }

    private void OnAlertSelectAttempt(ref AlertLevelSelectAttemptEvent ev)
    {
        var gate = EnsureComp<AlertLevelGateComponent>(ev.Station);
        if (ev.Level != gate.GatedLevel)
            return;

        if (!gate.Unlocked)
        {
            _popup.PopupEntity(
                Loc.GetString("alert-level-gate-locked"),
                ev.Console,
                ev.User,
                PopupType.MediumCaution);
            ev.Cancelled = true;
        }
    }

    private void OnGetVerbs(Entity<CommunicationsConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var station = _station.GetOwningStation(ent.Owner);
        if (station == null
            || !TryComp<AlertLevelGateComponent>(station, out var gate)
            || gate.Unlocked)
            return;

        var user = args.User;
        var console = ent.Owner;
        var stationUid = station.Value;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("alert-level-gate-verb-text"),
            Message = Loc.GetString("alert-level-gate-verb-message"),
            Priority = -1,
            Act = () =>
            {
                if (!TryComp<AlertLevelGateComponent>(stationUid, out var currentGate)
                    || currentGate.Unlocked
                    || !TryAuthorizeAlertLevel(stationUid, currentGate, user, console))
                    return;

                if (UnlockAlertLevelGate(stationUid, true))
                {
                    _popup.PopupEntity(
                        Loc.GetString("alert-level-gate-unlocked"),
                        console,
                        user,
                        PopupType.Medium);
                }
            },
        });
    }

    /// <summary>
    /// Runs the two-card command authorization.
    /// </summary>
    private bool TryAuthorizeAlertLevel(
        EntityUid station,
        AlertLevelGateComponent gate,
        EntityUid user,
        EntityUid console)
    {
        ExpirePending(gate);

        if (!_idCard.TryFindIdCard(user, out var idCard))
        {
            _popup.PopupEntity(
                Loc.GetString("alert-level-gate-no-id"),
                console,
                user,
                PopupType.MediumCaution);
            return false;
        }

        var tags = _accessReader.FindAccessTags(idCard);
        var isCommandHead = gate.InitiatorAccess.Any(tags.Contains);
        var isCommand = isCommandHead || tags.Contains(gate.CommandAccess);

        if (gate.PendingCard == null)
        {
            if (!isCommandHead)
            {
                _popup.PopupEntity(
                    Loc.GetString("alert-level-gate-needs-command"),
                    console,
                    user,
                    PopupType.MediumCaution);
                return false;
            }

            gate.PendingCard = idCard.Owner;
            gate.PendingExpiry = _timing.CurTime + gate.PendingTimeout;

            _popup.PopupEntity(
                Loc.GetString("alert-level-gate-first-swipe"),
                console,
                user,
                PopupType.Medium);

            AnnounceAuthorization(
                gate,
                console,
                idCard.Comp.FullName,
                "alert-level-gate-authorized-initiated-announcement");

            return false;
        }

        if (gate.PendingCard == idCard.Owner)
        {
            _popup.PopupEntity(
                Loc.GetString("alert-level-gate-same-id"),
                console,
                user,
                PopupType.MediumCaution);
            return false;
        }

        if (!isCommand)
        {
            _popup.PopupEntity(
                Loc.GetString("alert-level-gate-needs-second-command"),
                console,
                user,
                PopupType.MediumCaution);
            return false;
        }

        gate.PendingCard = null;
        gate.PendingExpiry = null;

        AnnounceAuthorization(
            gate,
            console,
            idCard.Comp.FullName,
            "alert-level-gate-authorized-announcement");

        return true;
    }

    private void AnnounceAuthorization(
        AlertLevelGateComponent gate,
        EntityUid console,
        string? name,
        string locId)
    {
        var announcement = Loc.GetString(
            locId,
            ("name", name ?? Loc.GetString("alert-level-gate-unknown-name")));

        _radio.SendRadioMessage(
            console,
            announcement,
            gate.CommandChannel,
            console);
    }

    private void ExpirePending(AlertLevelGateComponent gate)
    {
        if (gate.PendingExpiry != null && _timing.CurTime > gate.PendingExpiry)
            ClearPending(gate);
    }

    private static void ClearPending(AlertLevelGateComponent gate)
    {
        gate.PendingCard = null;
        gate.PendingExpiry = null;
    }
}
