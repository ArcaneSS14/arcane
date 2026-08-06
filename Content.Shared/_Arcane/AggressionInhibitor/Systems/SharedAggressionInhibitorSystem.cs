using Content.Shared._Arcane.AggressionInhibitor.Components;
using Robust.Shared.Containers;
using Content.Shared.Popups;
using Content.Shared.Examine;
using Content.Shared.Inventory.Events;
using Content.Shared.Access.Components;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using Content.Shared.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Content.Shared.CombatMode;
using Content.Shared.Inventory;
using Content.Shared.Electrocution;

namespace Content.Shared._Arcane.AggressionInhibitor.Systems;

public sealed partial class SharedAggressionInhibitorSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
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
        if (args.Handled || _netManager.IsClient)
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
            var slotItem = containerSlot.ContainedEntity;
            if (slotItem == null)
                continue;

            var uid = slotItem.Value;

            if (_inventorySystem.TryGetContainingSlot(uid, out var slotDef))
            {
                if ((slotDef.SlotFlags & SlotFlags.POCKET) != 0)
                    continue;
            }

            if (TryComp<AggressionInhibitorComponent>(uid, out var comp) && comp.IsActive)
            {
                inhibitorItem = uid;
                inhibitorComp = comp;
                break;
            }
        }

        if (inhibitorComp == null)
            return;

        if (!_combatMode.IsInCombatMode(user))
        {
            _electrocution.TryDoElectrocution(user, inhibitorItem, inhibitorComp.Damage, TimeSpan.FromSeconds(inhibitorComp.TimeStun), refresh: false, ignoreInsulation: true);

            _popup.PopupEntity(Loc.GetString("stabikor-disarm-shock-popup"), user, user, PopupType.LargeCaution);

            _combatMode.SetInCombatMode(user, false);

            args.Handled = true;
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
        if (comp.IsActive)
        {
            if (_netManager.IsServer)
            {
                comp.Timer = 0;
                Dirty(uid, comp);
            }
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

        if (comp.IsLocked && comp.Timer > 0)
        {
            var remainingTotalMinutes = (int) (comp.Timer / 60f);
            var remainingHours = remainingTotalMinutes / 60;
            var remainingMinutes = remainingTotalMinutes % 60;
            var remainingSeconds = (int) (comp.Timer % 60f);

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

        if (!_handsSystem.TryGetActiveItem(args.User, out var heldItem) ||
            !_idCard.TryFindIdCard(heldItem.Value, out var idCard) ||
            !TryComp<AccessComponent>(idCard.Owner, out var accessComp))
            return;

        var settingsIcon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png"));

        if (!comp.IsLocked)
        {
            args.Verbs.Add(new ActivationVerb()
            {
                Text = Loc.GetString("stabikor-verb-set-duration"),
                Icon = settingsIcon,
                Act = () => FlipOpenDialog(uid, args.User, comp)
            });
        }

        var verbText = comp.IsLocked ? "stabikor-verb-unlock" : "stabikor-verb-lock";

        args.Verbs.Add(new ActivationVerb()
        {
            Text = Loc.GetString(verbText),
            Icon = settingsIcon,
            Act = () => FlipToggleLock(uid, args.User, comp)
        });
    }

    private void FlipOpenDialog(EntityUid uid, EntityUid user, AggressionInhibitorComponent comp)
    {
        if (_gameTiming.CurTime < comp.LastVerbClickTime + TimeSpan.FromSeconds(0.4))
            return;

        comp.LastVerbClickTime = _gameTiming.CurTime;

        RaiseLocalEvent(uid, new OpenDialogEvent(uid, user));
    }

    private void FlipToggleLock(EntityUid uid, EntityUid user, AggressionInhibitorComponent comp)
    {
        if (_gameTiming.CurTime < comp.LastVerbClickTime + TimeSpan.FromSeconds(0.4))
            return;

        comp.LastVerbClickTime = _gameTiming.CurTime;

        RaiseLocalEvent(uid, new ToggleLockEvent(uid, user));
    }
}
