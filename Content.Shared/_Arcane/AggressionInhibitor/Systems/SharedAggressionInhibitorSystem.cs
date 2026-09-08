using Content.Shared._Arcane.AggressionInhibitor.Components;
using Robust.Shared.Containers;
using Content.Shared.Popups;
using Content.Shared.Examine;
using Content.Shared.Inventory.Events;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Content.Shared.CombatMode;
using Content.Shared.Inventory;
using Content.Shared.Electrocution;

namespace Content.Shared._Arcane.AggressionInhibitor.Systems;

public sealed partial class SharedAggressionInhibitorSystem : EntitySystem
{
    private static SpriteSpecifier.Texture _settingsIcon = new(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png"));
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private SharedCombatModeSystem _combatMode = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleCombatActionEvent>(OnToggleCombatAction, before: [typeof(SharedCombatModeSystem)]);
        SubscribeLocalEvent<AggressionInhibitorComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<AggressionInhibitorComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<AggressionInhibitorComponent, BeingUnequippedAttemptEvent>(OnBeingUnequippedAttempt);
        SubscribeLocalEvent<AggressionInhibitorComponent, GetVerbsEvent<ActivationVerb>>(OnGetActivationVerbs);
    }

    private void OnToggleCombatAction(ToggleCombatActionEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;

        if (TryComp<Mobs.Components.MobStateComponent>(user, out var mobState))
        {
            if (mobState.CurrentState == Mobs.MobState.Critical ||
                mobState.CurrentState == Mobs.MobState.Dead)
                return;
        }

        EntityUid inhibitorItem = default;
        AggressionInhibitorComponent? inhibitorComp = null;

        var slotEnumerator = _inventorySystem.GetSlotEnumerator(user);
        while (slotEnumerator.MoveNext(out var containerSlot))
        {
            if (containerSlot.ContainedEntity is not { } slotItem)
                continue;

            if (_inventorySystem.TryGetContainingSlot(slotItem, out var slotDef))
            {
                if (slotDef.SlotFlags.HasFlag(SlotFlags.POCKET))
                    continue;
            }

            if (TryComp<AggressionInhibitorComponent>(slotItem, out var comp) && comp.IsActive)
            {
                inhibitorItem = slotItem;
                inhibitorComp = comp;
                break;
            }
        }

        if (inhibitorComp == null)
            return;

        if (!_combatMode.IsInCombatMode(user))
        {
            _combatMode.SetInCombatMode(user, false);
            args.Handled = true;

            if (_netManager.IsServer)
            {
                _electrocution.TryDoElectrocution(user, inhibitorItem, inhibitorComp.Damage, TimeSpan.FromSeconds(inhibitorComp.TimeStun), refresh: false, ignoreInsulation: true);

                _popup.PopupEntity(Loc.GetString("stabikor-disarm-shock-popup"), user, user, PopupType.LargeCaution);
            }
        }
    }

    private void OnBeingUnequippedAttempt(EntityUid uid, AggressionInhibitorComponent comp, BeingUnequippedAttemptEvent args)
    {
        if (comp.IsLocked || comp.IsActive)
        {
            _popup.PopupPredicted(Loc.GetString("stabikor-unequip-blocked-active"), uid, args.Unequipee, PopupType.SmallCaution);
            args.Cancel();
        }
    }

    private void OnGotUnequipped(EntityUid uid, AggressionInhibitorComponent comp, ref GotUnequippedEvent args)
    {
        if (comp.IsActive && _netManager.IsServer)
        {
            comp.NextUpdate = default;
            Dirty(uid, comp);
        }
    }

    private void OnExamine(EntityUid uid, AggressionInhibitorComponent comp, ExaminedEvent args)
    {
        var state = comp.IsLocked ? "stabikor-examine-locked" : "stabikor-examine-unlocked";
        args.PushMarkup(Loc.GetString("stabikor-examine-status-main", ("mode", Loc.GetString(state))));

        var durationTotalMinutes = (int) (comp.Duration / 60f);
        var durationHours = durationTotalMinutes / 60;
        var durationMinutes = durationTotalMinutes % 60;

        args.PushMarkup(Loc.GetString("stabikor-examine-duration-info",
            ("hours", durationHours),
            ("minutes", durationMinutes)));

        var remaining = comp.NextUpdate - _timing.CurTime;

        if (comp.IsLocked && remaining.Ticks > 0)
        {
            var remainingHours = (int) remaining.TotalHours;
            var remainingMinutes = remaining.Minutes;
            var remainingSeconds = remaining.Seconds;

            args.PushMarkup(Loc.GetString("stabikor-examine-timer-remaining",
                ("hours", remainingHours),
                ("minutes", remainingMinutes),
                ("seconds", remainingSeconds)));
        }
    }

    private void OnGetActivationVerbs(EntityUid uid, AggressionInhibitorComponent comp, GetVerbsEvent<ActivationVerb> args)
    {
        var isInContainer = _containerSystem.TryGetContainingContainer((uid, null, null), out var container)
            && container.Owner.IsValid();

        if (!isInContainer && (!args.CanAccess || !args.CanInteract))
            return;

        if (!comp.IsLocked)
        {
            args.Verbs.Add(new ActivationVerb()
            {
                Text = Loc.GetString("stabikor-verb-set-duration"),
                Icon = _settingsIcon,
                Act = () => FlipOpenDialog(uid, args.User, comp)
            });
        }

        var verbText = comp.IsLocked ? "stabikor-verb-unlock" : "stabikor-verb-lock";

        args.Verbs.Add(new ActivationVerb()
        {
            Text = Loc.GetString(verbText),
            Icon = _settingsIcon,
            Act = () => FlipToggleLock(uid, args.User, comp)
        });
    }

    private void FlipOpenDialog(EntityUid uid, EntityUid user, AggressionInhibitorComponent comp)
    {
        if (_timing.CurTime < comp.LastVerbClickTime + TimeSpan.FromSeconds(0.4))
            return;

        comp.LastVerbClickTime = _timing.CurTime;

        RaiseLocalEvent(uid, new OpenDialogEvent(uid, user));
    }

    private void FlipToggleLock(EntityUid uid, EntityUid user, AggressionInhibitorComponent comp)
    {
        if (_timing.CurTime < comp.LastVerbClickTime + TimeSpan.FromSeconds(0.4))
            return;

        comp.LastVerbClickTime = _timing.CurTime;

        RaiseLocalEvent(uid, new ToggleLockEvent(uid, user));
    }
}
