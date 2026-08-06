using Content.Shared._Arcane.AggressionInhibitor.Components;
using Robust.Shared.Containers;
using Content.Shared.CombatMode;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Shared.Access.Components;
using Content.Server.Access.Systems;
using Robust.Shared.Audio.Systems;
using Content.Server.Administration;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Server.Player;

namespace Content.Server._Arcane.AggressionInhibitor.Systems;

public sealed partial class AggressionInhibitorSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IdCardSystem _idCard = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private QuickDialogSystem _quickDialog = default!;
    [Dependency] private SharedCombatModeSystem _combatMode = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AggressionInhibitorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<AggressionInhibitorComponent, OpenDialogEvent>(OnOpenDialogReceived);
        SubscribeLocalEvent<AggressionInhibitorComponent, ToggleLockEvent>(OnToggleLockReceived);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AggressionInhibitorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsActive || comp.WearingEntity == null)
                continue;

            comp.Timer -= frameTime;

            if (comp.Timer <= 0)
            {
                if (!RemoveInhibitor(uid, comp))
                    continue;
            }
            Dirty(uid, comp);
        }
    }

    private void OnInteractUsing(EntityUid uid, AggressionInhibitorComponent comp, InteractUsingEvent args)
    {
        var user = args.User;

        var parent = Transform(uid).ParentUid;
        var isEquipped = parent.IsValid() && HasComp<ContainerManagerComponent>(parent);

        if (!isEquipped)
        {
            _audio.PlayPvs(comp.DenySound, uid);

            args.Handled = true;
            return;
        }

        if (!_idCard.TryFindIdCard(args.Used, out var idCard) ||
            !TryComp<AccessComponent>(idCard.Owner, out var accessComp))
            return;

        var cardAccess = accessComp.Tags;

        if (comp.IsLocked)
        {
            var hasUnlockAccess = comp.UnlockAccess.Exists(proto => cardAccess.Contains(proto.Id));

            if (hasUnlockAccess)
            {
                if (!RemoveInhibitor(uid, comp))
                    return;

                args.Handled = true;
                return;
            }
            else _audio.PlayPvs(comp.DenySound, uid);
        }
        else
        {
            var hasLockAccess = comp.LockAccess.Exists(proto => cardAccess.Contains(proto.Id));

            if (hasLockAccess)
            {
                if (!ActivateInhibitor(uid, parent, comp, user))
                    return;

                args.Handled = true;
                return;
            }
            else _audio.PlayPvs(comp.DenySound, uid);
        }
    }

    public void OpenDialog(EntityUid uid, AggressionInhibitorComponent comp, EntityUid user)
    {
        EntityUid? parent = _containerSystem.TryGetContainingContainer((uid, null, null), out var container)
            ? container.Owner
            : null;

        var targetEntity = parent ?? uid;
        if (!_transformSystem.InRange(user, targetEntity, 2f))
            return;

        if (!_handsSystem.TryGetActiveItem(user, out var heldItem) ||
            !_idCard.TryFindIdCard(heldItem.Value, out var idCard) ||
            !TryComp<AccessComponent>(idCard.Owner, out var accessComp))
            return;

        if (comp.IsLocked)
            return;

        var cardAccess = accessComp.Tags;
        var hasSettingsAccess = comp.LockAccess.Exists(proto => cardAccess.Contains(proto.Id));

        if (!hasSettingsAccess)
        {
            _audio.PlayPvs(comp.DenySound, uid);
            return;
        }

        if (!_playerManager.TryGetSessionByEntity(user, out var session))
            return;

        _quickDialog.OpenDialog(session, Loc.GetString("stabikor-dialog-title"), Loc.GetString("stabikor-dialog-field") + "\n", (string input) =>
        {
            if (!EntityManager.EntityExists(uid) || comp.IsLocked)
                return;

            if (string.IsNullOrEmpty(input))
            {
                comp.Duration = 60f;
                Dirty(uid, comp);
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

    public void ToggleLock(EntityUid uid, AggressionInhibitorComponent comp, EntityUid user)
    {
        EntityUid? parent = _containerSystem.TryGetContainingContainer((uid, null, null), out var container)
            ? container.Owner
            : null;

        var targetEntity = parent ?? uid;
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
                if (!RemoveInhibitor(uid, comp))
                    return;
            }
            else _audio.PlayPvs(comp.DenySound, uid);
        }
        else
        {
            if (comp.LockAccess.Exists(proto => cardAccess.Contains(proto.Id)))
            {
                if (!ActivateInhibitor(uid, parent ?? uid, comp, user))
                    return;
            }
            else _audio.PlayPvs(comp.DenySound, uid);
        }
    }

    private bool ActivateInhibitor(EntityUid uid, EntityUid wearerUid, AggressionInhibitorComponent comp, EntityUid user)
    {
        if (comp.IsActive)
            return false;

        if (_inventorySystem.TryGetContainingSlot(uid, out var slotDef))
        {
            if ((slotDef.SlotFlags & SlotFlags.POCKET) != 0)
                return false;

            if (_inventorySystem.TryGetSlotEntity(wearerUid, slotDef.Name, out var slotItem) && slotItem == uid)
            {
                comp.Timer = comp.Duration;
                comp.IsLocked = true;
                comp.IsActive = true;
                comp.WearingEntity = wearerUid;
                _combatMode.SetInCombatMode(user, false);

                Dirty(uid, comp);

                _audio.PlayPvs(comp.LockSound, uid);

                _popup.PopupEntity(Loc.GetString("stabikor-activated-success", ("item", uid), ("user", Name(wearerUid))), uid);

                return true;
            }
        }
        _audio.PlayPvs(comp.DenySound, uid);

        _popup.PopupEntity(Loc.GetString("stabikor-not-equipped"), uid, user);
        return false;
    }

    private bool RemoveInhibitor(EntityUid uid, AggressionInhibitorComponent comp)
    {
        if (comp.WearingEntity is not { Valid: true } user)
            return false;

        if (_inventorySystem.TryGetContainingSlot(uid, out var slotDef))
        {
            if (!_inventorySystem.TryUnequip(user, user, slotDef.Name, force: true))
                return false;
        }

        comp.Timer = 0f;
        comp.IsLocked = false;
        comp.IsActive = false;
        comp.WearingEntity = null;

        Dirty(uid, comp);

        _audio.PlayPvs(comp.UnlockSound, uid);

        _popup.PopupEntity(Loc.GetString("stabikor-moment-shutdown", ("item", uid)), uid);

        return true;
    }

    private void OnOpenDialogReceived(EntityUid uid, AggressionInhibitorComponent comp, OpenDialogEvent args)
    {
        OpenDialog(uid, comp, args.User);
    }

    private void OnToggleLockReceived(EntityUid uid, AggressionInhibitorComponent comp, ToggleLockEvent args)
    {
        ToggleLock(uid, comp, args.User);
    }
}
