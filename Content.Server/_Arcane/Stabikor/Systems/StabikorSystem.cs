using Content.Shared._Arcane.Stabikor.Components;
using Robust.Shared.Containers;
using Content.Shared.CombatMode;
using Content.Server.Electrocution;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Shared.Access.Components;
using Content.Server.Access.Systems;
using Robust.Shared.Audio.Systems;
using Content.Server.Administration;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Server.Player;

namespace Content.Server._Arcane.Stabikor.Systems;

public sealed partial class StabikorSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleCombatActionEvent>(OnToggleCombatAction);
        SubscribeLocalEvent<StabikorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeNetworkEvent<OpenDialogEvent>(OnOpenDialogNetworkReceived);
        SubscribeNetworkEvent<ToggleLockEvent>(OnIsLockedNetworkReceived);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StabikorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsActive || comp.WearingEntity == null)
                continue;

            comp.Timer -= frameTime;

            if (comp.Timer <= 0)
            {
                if (!RemoveStabikor(uid, comp))
                    return;
            }
        }
    }

    private void OnToggleCombatAction(ToggleCombatActionEvent args)
    {
        var user = args.Performer;

        if (TryComp<Shared.Mobs.Components.MobStateComponent>(user, out var mobState))
        {
            if (mobState.CurrentState == Shared.Mobs.MobState.Critical ||
                mobState.CurrentState == Shared.Mobs.MobState.Dead)
            {
                return;
            }
        }

        var targetSlots = new[] { "neck", "gloves" };
        EntityUid stabikorItem = default;
        StabikorComponent? stabikorComp = null;

        foreach (var slot in targetSlots)
        {
            if (_inventorySystem.TryGetSlotEntity(user, slot, out var item) &&
                TryComp<StabikorComponent>(item, out var comp))
            {
                stabikorItem = item!.Value;
                stabikorComp = comp;
                break;
            }
        }

        if (stabikorComp is not { IsActive: true })
            return;

        if (args.Action.Comp.Enabled)
        {
            _electrocution.TryDoElectrocution(user, stabikorItem, stabikorComp.Damage, TimeSpan.FromSeconds(stabikorComp.TimeStan), refresh: false, ignoreInsulation: true);

            _popup.PopupEntity(Loc.GetString("stabikor-disarm-shock-popup"), user, user, PopupType.LargeCaution);

            _combatMode.SetInCombatMode(user, false);

            args.Handled = true;
        }
    }

    private void OnInteractUsing(EntityUid uid, StabikorComponent comp, InteractUsingEvent args)
    {
        var user = args.User;

        var parent = Transform(uid).ParentUid;
        var isEquipped = parent.IsValid() && HasComp<ContainerManagerComponent>(parent);

        if (!isEquipped)
        {
            _popup.PopupEntity(Loc.GetString("stabikor-not-equipped"), uid, user);
            _audio.PlayPvs(comp.DenySound, uid);

            return;
        }

        if (!_idCard.TryFindIdCard(args.Used, out var idCard) ||
            !TryComp<AccessComponent>(idCard.Owner, out var accessComp))
        {
            return;
        }

        var cardAccess = accessComp.Tags;

        if (comp.IsLocked)
        {
            var hasUnlockAccess = comp.UnlockAccess.Exists(proto => cardAccess.Contains(proto.Id));

            if (hasUnlockAccess)
            {
                if (RemoveStabikor(uid, comp))
                    _audio.PlayPvs(comp.UnlockSound, uid);

                args.Handled = true;
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("stabikor-unlocked-deny"), uid, user, PopupType.SmallCaution);

                _audio.PlayPvs(comp.DenySound, uid);
            }
        }
        else
        {
            var hasLockAccess = comp.LockAccess.Exists(proto => cardAccess.Contains(proto.Id));

            if (hasLockAccess)
            {
                if (ActivateStabikor(uid, parent, comp, user))
                    _audio.PlayPvs(comp.LockSound, uid);

                args.Handled = true;
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("stabikor-locked-deny"), uid, user, PopupType.SmallCaution);

                _audio.PlayPvs(comp.DenySound, uid);
            }
        }
    }

    public void OpenDialog(EntityUid uid, StabikorComponent comp, EntityUid user)
    {

        _containerSystem.TryGetContainingContainer((uid, null, null), out var container);
        var parent = container?.Owner ?? EntityUid.Invalid;
        var isInContainer = parent.IsValid();

        var targetEntity = isInContainer ? parent : uid;
        if (!_transformSystem.InRange(user, targetEntity, 2f))
            return;

        if (!_handsSystem.TryGetActiveItem(user, out var heldItem) ||
            !_idCard.TryFindIdCard(heldItem.Value, out var idCard) ||
            !TryComp<AccessComponent>(idCard.Owner, out var accessComp))
            return;

        if (comp.IsLocked)
            return;

        if (!_playerManager.TryGetSessionByEntity(user, out var session))
            return;

        _quickDialog.OpenDialog(session, Loc.GetString("stabikor-dialog-title"), Loc.GetString("stabikor-dialog-field") + "\n", (string input) =>
        {
            if (!EntityManager.EntityExists(uid))
                return;

            if (string.IsNullOrEmpty(input))
            {
                comp.Duration = 60f;
                _popup.PopupEntity(Loc.GetString("stabikor-duration-set-cancel-fallback", ("time", 1)), uid, user);
                return;
            }

            if (!int.TryParse(input, out var durationMinutes) || durationMinutes < 1 || durationMinutes > 900)
            {
                _popup.PopupEntity(Loc.GetString("stabikor-dialog-invalid-range"), user, user, PopupType.SmallCaution);
                _audio.PlayPvs(comp.DenySound, uid);
                return;
            }

            comp.Duration = durationMinutes * 60f;
            _popup.PopupEntity(Loc.GetString("stabikor-duration-set-success", ("time", durationMinutes)), uid, user);

            _audio.PlayPvs(comp.UnlockSound, uid);
            Dirty(uid, comp);
        });
    }

    public void IsLocked(EntityUid uid, StabikorComponent comp, EntityUid user)
    {
        _containerSystem.TryGetContainingContainer((uid, null, null), out var container);
        var parent = container?.Owner ?? EntityUid.Invalid;
        var isInContainer = parent.IsValid();

        var targetEntity = isInContainer ? parent : uid;
        if (!_transformSystem.InRange(user, targetEntity, 2f))
            return;

        if (!_handsSystem.TryGetActiveItem(user, out var heldItem) ||
            !_idCard.TryFindIdCard(heldItem.Value, out var idCard) ||
            !TryComp<AccessComponent>(idCard.Owner, out var accessComp))
            return;

        var cardAccess = accessComp.Tags;

        if (comp.IsLocked)
        {
            if (comp.UnlockAccess.Exists(proto => cardAccess.Contains(proto.Id)))
            {
                if (!RemoveStabikor(uid, comp))
                    return;
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("stabikor-unlocked-deny"), uid, user, PopupType.SmallCaution);
                _audio.PlayPvs(comp.DenySound, uid);
            }
        }
        else
        {
            if (comp.LockAccess.Exists(proto => cardAccess.Contains(proto.Id)))
            {
                if (!ActivateStabikor(uid, parent, comp, user))
                    return;
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("stabikor-locked-deny"), uid, user, PopupType.SmallCaution);
                _audio.PlayPvs(comp.DenySound, uid);
            }
        }
    }

    private bool ActivateStabikor(EntityUid uid, EntityUid wearerUid, StabikorComponent comp, EntityUid user)
    {
        if (comp.IsActive)
            return false;

        if (_inventorySystem.TryGetSlotEntity(wearerUid, "gloves", out var glovesItem) && glovesItem == uid ||
         _inventorySystem.TryGetSlotEntity(wearerUid, "neck", out var neckItem) && neckItem == uid)
        {
            comp.Timer = comp.Duration;
            comp.IsLocked = true;
            comp.IsActive = true;
            comp.WearingEntity = wearerUid;
            Dirty(uid, comp);

            _audio.PlayPvs(comp.LockSound, uid);

            _popup.PopupEntity(Loc.GetString("stabikor-activated-success", ("item", uid), ("user", Loc.GetString(Name(wearerUid)))), uid);

            return true;
        }

        _audio.PlayPvs(comp.DenySound, uid);

        _popup.PopupEntity(Loc.GetString("stabikor-not-equipped"), uid, user);
        return false;
    }

    private bool RemoveStabikor(EntityUid uid, StabikorComponent comp)
    {
        comp.Timer = 0f;
        comp.IsLocked = false;
        comp.IsActive = false;

        if (comp.WearingEntity is not { Valid: true } user)
        {
            Dirty(uid, comp);
            return false;
        }
        comp.WearingEntity = null;
        Dirty(uid, comp);

        if (!_inventorySystem.TryGetContainingSlot(uid, out var slotDef))
            return false;

        if (!_inventorySystem.TryUnequip(user, slotDef.Name, force: true))
            return false;

        _audio.PlayPvs(comp.UnlockSound, uid);

        _popup.PopupEntity(Loc.GetString("stabikor-moment-shutdown", ("item", uid), ("user", Loc.GetString(Name(user)))), uid);

        return true;
    }

    private void OnOpenDialogNetworkReceived(OpenDialogEvent msg, EntitySessionEventArgs args)
    {
        var uid = GetEntity(msg.Verp);
        var user = args.SenderSession.AttachedEntity;

        if (user == null)
            return;

        if (TryComp<StabikorComponent>(uid, out var comp))
            OpenDialog(uid, comp, user.Value);
    }

    private void OnIsLockedNetworkReceived(ToggleLockEvent msg, EntitySessionEventArgs args)
    {
        var uid = GetEntity(msg.Verp);

        var user = args.SenderSession.AttachedEntity;

        if (user == null)
            return;

        if (TryComp<StabikorComponent>(uid, out var comp))
            IsLocked(uid, comp, user.Value);
    }
}
