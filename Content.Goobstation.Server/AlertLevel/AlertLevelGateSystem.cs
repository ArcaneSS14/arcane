using System.Linq;
using Content.Goobstation.Shared.AlertLevel;
using Content.Goobstation.Shared.Shadowling;
using Content.Goobstation.Shared.Slasher;
using Content.Server.Access.Systems;
using Content.Server.Communications;
using Content.Server.GameTicking.Rules;
using Content.Server.NukeOps;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared._White.Xenomorphs;
using Content.Server.AlertLevel;
using Content.Shared.Emp;
using Content.Shared.Heretic.Prototypes;
using Content.Shared.NukeOps;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.AlertLevel;

/// <summary>
/// Controls whether the gated alert level is unlocked.
/// </summary>
public sealed class AlertLevelGateSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
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

    /// <summary>
    /// Unlocks the gated alert level, allowing it to be manually activated from a
    /// communications console. Called when a qualifying threat occurs.
    /// </summary>
    public void UnlockAlertLevelGate()
    {
        var query = EntityQueryEnumerator<AlertLevelComponent>();
        while (query.MoveNext(out var station, out _))
        {
            var gate = EnsureComp<AlertLevelGateComponent>(station);
            if (gate.Unlocked)
                continue;

            gate.Unlocked = true;
            _radio.SendRadioMessage(
                station,
                Loc.GetString("alert-level-gate-unlocked-announcement"),
                gate.CommandChannel,
                station);
            PlayUnlockSound(gate, station);
        }
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
                    || !TryAuthorizeAlertLevel(currentGate, user, console))
                    return;

                currentGate.Unlocked = true;
                _popup.PopupEntity(
                    Loc.GetString("alert-level-gate-unlocked"),
                    console,
                    user,
                    PopupType.Medium);
            },
        });
    }

    /// <summary>
    /// Runs the two-card command authorization.
    /// </summary>
    private bool TryAuthorizeAlertLevel(AlertLevelGateComponent gate, EntityUid user, EntityUid console)
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

        PlayUnlockSound(gate, console);
    }

    private void PlayUnlockSound(AlertLevelGateComponent gate, EntityUid source)
    {
        if (!IsCommandChannelUp(gate, source))
            return;

        var filter = Filter.Empty()
            .AddWhereAttachedEntity(entity => HasCommandComms(gate, entity));

        _audio.PlayGlobal(gate.UnlockSound, filter, true);
    }

    private bool HasCommandComms(AlertLevelGateComponent gate, EntityUid entity)
    {
        return TryComp<WearingHeadsetComponent>(entity, out var wearing)
            && TryComp<ActiveRadioComponent>(wearing.Headset, out var radio)
            && (radio.ReceiveAllChannels || radio.Channels.Contains(gate.CommandChannel))
            && !HasComp<EmpDisabledComponent>(wearing.Headset);
    }

    private bool IsCommandChannelUp(AlertLevelGateComponent gate, EntityUid console)
    {
        var channel = _prototype.Index(gate.CommandChannel);
        if (channel.LongRange)
            return true;

        var mapId = Transform(console).MapID;
        var query = EntityQueryEnumerator<
            TelecomServerComponent,
            EncryptionKeyHolderComponent,
            ApcPowerReceiverComponent,
            TransformComponent>();

        while (query.MoveNext(out _, out _, out var keys, out var power, out var transform))
        {
            if (transform.MapID == mapId
                && power.Powered
                && keys.Channels.Contains(gate.CommandChannel))
                return true;
        }

        return false;
    }

    private void ExpirePending(AlertLevelGateComponent gate)
    {
        if (gate.PendingExpiry != null && _timing.CurTime > gate.PendingExpiry)
        {
            gate.PendingCard = null;
            gate.PendingExpiry = null;
        }
    }
}
