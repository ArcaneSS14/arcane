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
using Robust.Shared.Timing;

namespace Content.Server._Arcane.AggressionInhibitor.Systems;

public sealed partial class AggressionInhibitorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
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

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AggressionInhibitorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate || !comp.IsActive || comp.WearingEntity == null)
                continue;

            if (!RemoveInhibitor(uid, comp))
                continue;

            Dirty(uid, comp);
        }
    }

    private void OnInteractUsing(EntityUid uid, AggressionInhibitorComponent comp, InteractUsingEvent args)
    {
        var user = args.User;

        if (!TryComp<TransformComponent>(uid, out var xform))
            return;

        var parent = xform.ParentUid;

        if (!parent.IsValid() && HasComp<ContainerManagerComponent>(parent))
        {
            PlaybackDenySound(uid, comp);

            args.Handled = true;
            return;
        }

        if (!_idCard.TryFindIdCard(args.Used, out var idCard) ||
            !TryComp<AccessComponent>(idCard.Owner, out var accessComp))
            return;

        if (comp.IsLocked)
        {
            if (GetHasUnlockAccess(comp, accessComp.Tags))
            {
                if (!RemoveInhibitor(uid, comp))
                    return;

                args.Handled = true;
                return;
            }
            else
                PlaybackDenySound(uid, comp);
        }
        else
        {
            if (GetHasLockAccess(comp, accessComp.Tags))
            {
                if (!ActivateInhibitor(uid, parent, comp, user))
                    return;

                args.Handled = true;
                return;
            }
            else
                PlaybackDenySound(uid, comp);
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

        if (!GetHasLockAccess(comp, accessComp.Tags))
        {
            PlaybackDenySound(uid, comp);
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
                comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(comp.Duration);

                Dirty(uid, comp);
                _popup.PopupEntity(Loc.GetString("stabikor-duration-set-cancel-fallback", ("time", 1)), uid, user);
                return;
            }

            if (!int.TryParse(input, out var durationMinutes) || durationMinutes < 1 || durationMinutes > 900)
            {
                _popup.PopupEntity(Loc.GetString("stabikor-dialog-invalid-range"), user, user, PopupType.SmallCaution);
                PlaybackDenySound(uid, comp);
                return;
            }

            comp.Duration = durationMinutes * 60f;
            comp.NextUpdate = _timing.CurTime + TimeSpan.FromMinutes(durationMinutes);

            _popup.PopupEntity(Loc.GetString("stabikor-duration-set-success", ("time", durationMinutes)), uid, user);
            PlaybackUnlockSound(uid, comp);

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

        if (comp.IsLocked)
        {
            if (GetHasUnlockAccess(comp, accessComp.Tags))
            {
                if (!RemoveInhibitor(uid, comp))
                    return;
            }
            else
                PlaybackDenySound(uid, comp);
        }
        else
        {
            if (GetHasLockAccess(comp, accessComp.Tags))
            {
                if (!ActivateInhibitor(uid, parent ?? uid, comp, user))
                    return;
            }
            else
                PlaybackDenySound(uid, comp);
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
                comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(comp.Duration);
                comp.IsLocked = true;
                comp.IsActive = true;
                comp.WearingEntity = wearerUid;
                _combatMode.SetInCombatMode(wearerUid, false);

                Dirty(uid, comp);

                PlaybackLockSound(uid, comp);

                _popup.PopupEntity(Loc.GetString("stabikor-activated-success", ("item", uid), ("user", Name(wearerUid))), uid);

                return true;
            }
        }
        PlaybackDenySound(uid, comp);

        _popup.PopupEntity(Loc.GetString("stabikor-not-equipped"), uid, user);
        return false;
    }

    private bool RemoveInhibitor(EntityUid uid, AggressionInhibitorComponent comp)
    {
        if (comp.WearingEntity is not { Valid: true } user)
            return false;

        if (_containerSystem.TryGetContainingContainer(uid, out var container))
        {
            if (!_containerSystem.TryRemoveFromContainer(uid, force: true))
                return false;

            _transformSystem.SetCoordinates(uid, _transformSystem.GetMoverCoordinates(user));
        }

        comp.NextUpdate = TimeSpan.MaxValue;
        comp.IsLocked = false;
        comp.IsActive = false;
        comp.WearingEntity = null;

        Dirty(uid, comp);

        PlaybackUnlockSound(uid, comp);

        _popup.PopupEntity(Loc.GetString("stabikor-moment-shutdown", ("item", uid)), uid);

        return true;
    }

    private static bool GetHasLockAccess(AggressionInhibitorComponent comp, HashSet<Robust.Shared.Prototypes.ProtoId<Shared.Access.AccessLevelPrototype>> cardAccess)
    {
        return comp.LockAccess.Exists(proto => cardAccess.Contains(proto.Id));
    }

    private static bool GetHasUnlockAccess(AggressionInhibitorComponent comp, HashSet<Robust.Shared.Prototypes.ProtoId<Shared.Access.AccessLevelPrototype>> cardAccess)
    {
        return comp.UnlockAccess.Exists(proto => cardAccess.Contains(proto.Id));
    }

    private void PlaybackDenySound(EntityUid uid, AggressionInhibitorComponent comp)
    {
        _audio.PlayPvs(comp.DenySound, uid);
    }

    private void PlaybackUnlockSound(EntityUid uid, AggressionInhibitorComponent comp)
    {
        _audio.PlayPvs(comp.UnlockSound, uid);
    }

    private void PlaybackLockSound(EntityUid uid, AggressionInhibitorComponent comp)
    {
        _audio.PlayPvs(comp.LockSound, uid);
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
