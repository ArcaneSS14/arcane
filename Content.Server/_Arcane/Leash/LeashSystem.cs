using Content.Shared._Arcane.Leash;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using System.Numerics;

namespace Content.Server._Arcane.Leash;

public sealed class LeashSystem : SharedLeashSystem
{
    [Dependency] private readonly SharedJointSystem _jointSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LeashComponent, DroppedEvent>(OnLeashDropped);
        SubscribeLocalEvent<LeashComponent, EntGotInsertedIntoContainerMessage>(OnLeashContainerInserted);
        // Teleportation
        SubscribeLocalEvent<LeashComponent, MoveEvent>(OnLeashMove);
        SubscribeLocalEvent<LeashedComponent, MoveEvent>(OnLeashedMove);
    }

    /// <summary>
    /// Reattaching the connection to the leash if it's been thrown away
    /// </summary>
    private void OnLeashDropped(EntityUid uid, LeashComponent component, ref DroppedEvent args)
    {
        if (component.AttachedEntity == null)
            return;

        ReanchorJoint(uid, component, newAnchor: uid);
    }

    /// <summary>
    /// Moving the leash into another creature's container or hand
    /// </summary>
    private void OnLeashContainerInserted(EntityUid uid, LeashComponent component, ref EntGotInsertedIntoContainerMessage args)
    {
        if (component.AttachedEntity == null)
            return;

        var containerOwner = args.Container.Owner;

        // If the leash has been handed over
        if (_handsSystem.IsHolding(containerOwner, uid, out _))
        {
            ReanchorJoint(uid, component, newAnchor: containerOwner);
        }
        else
        {
            // If the leash is in the container
            var attached = component.AttachedEntity;
            TryDetachLeash(uid, component);

            if (attached is { } leashed)
                _popupSystem.PopupEntity(Loc.GetString("leash-popup-detached-container"), leashed, leashed);
        }
    }

    /// <summary>
    /// Checks for a connection break when moving the lead
    /// </summary>
    private void OnLeashMove(EntityUid uid, LeashComponent component, ref MoveEvent args)
    {
        if (component.AttachedEntity is not { } target)
            return;

        CheckTeleportOrDistance(uid, component, target, ref args);
    }

    /// <summary>
    /// Checks for connection break when moving the collar
    /// </summary>
    private void OnLeashedMove(EntityUid uid, LeashedComponent component, ref MoveEvent args)
    {
        if (component.Leash is not { } leashUid)
            return;

        if (TryComp<LeashComponent>(leashUid, out var leashComp))
        {
            CheckTeleportOrDistance(leashUid, leashComp, uid, ref args);
        }
    }

    /// <summary>
    /// Instant distance check between objects with any of their shifts
    /// </summary>
    private void CheckTeleportOrDistance(EntityUid leashUid, LeashComponent leash, EntityUid targetUid, ref MoveEvent args)
    {
        if (!TryComp<TransformComponent>(leashUid, out var leashXform) ||
            !TryComp<TransformComponent>(targetUid, out var targetXform))
        {
            return;
        }

        // Check for map ID match
        if (leashXform.MapID != targetXform.MapID)
        {
            SnapLeash(leashUid, leash, targetUid);
            return;
        }

        var oldWorldPos = _transform.ToMapCoordinates(args.OldPosition).Position;
        var newWorldPos = _transform.ToMapCoordinates(args.NewPosition).Position;

        // If the distance between the old and new position is greater than the break distance, we break the connection
        if (Vector2.DistanceSquared(oldWorldPos, newWorldPos) > leash.SnapDistanceSq)
        {
            SnapLeash(leashUid, leash, targetUid);
            return;
        }

        var leashPos = _transform.GetWorldPosition(leashXform);
        var targetPos = _transform.GetWorldPosition(targetXform);

        if (Vector2.DistanceSquared(leashPos, targetPos) > leash.SnapDistanceSq)
        {
            SnapLeash(leashUid, leash, targetUid);
        }
    }

    /// <summary>
    /// Loss of connection during a sudden movement
    /// </summary>
    private void SnapLeash(EntityUid leashUid, LeashComponent leash, EntityUid targetUid)
    {
        TryDetachLeash(leashUid, leash);
        _popupSystem.PopupEntity(Loc.GetString("leash-popup-snap"), targetUid, targetUid);
    }

    /// <summary>
    /// Creating a bound entity constraint
    /// </summary>
    private void ReanchorJoint(EntityUid leashUid, LeashComponent leash, EntityUid newAnchor)
    {
        if (leash.AttachedEntity is not { } target)
            return;

        if (leash.JointId != null)
        {
            _jointSystem.RemoveJoint(target, leash.JointId);
            _jointSystem.RemoveJoint(leashUid, leash.JointId);
            _jointSystem.RemoveJoint(newAnchor, leash.JointId);
        }

        if (!HasComp<PhysicsComponent>(newAnchor) || !HasComp<PhysicsComponent>(target))
        {
            TryDetachLeash(leashUid, leash);
            return;
        }

        leash.JointId = $"leash_{leashUid}_{Guid.NewGuid()}";

        var joint = _jointSystem.CreateDistanceJoint(newAnchor, target, id: leash.JointId!);
        joint.MaxLength = leash.MaxDistance;
        joint.MinLength = 0f;
    }

    /// <summary>
    /// Leash attachment system with collar. Checks compliance with conditions
    /// </summary>
    public override bool TryAttachLeash(EntityUid leashUid, EntityUid userUid, EntityUid targetUid, LeashComponent? leash = null)
    {
        if (!base.TryAttachLeash(leashUid, userUid, targetUid, leash))
            return false;

        if (!Resolve(leashUid, ref leash) || leash.AttachedEntity is not { } targetEntity)
            return false;

        // Server physics and component state synchronization
        var joint = _jointSystem.CreateDistanceJoint(userUid, targetEntity, id: leash.JointId!);
        joint.MaxLength = leash.MaxDistance;
        joint.MinLength = 0f;

        Dirty(leashUid, leash);
        if (TryComp<LeashedComponent>(targetEntity, out var leashedComp))
        {
            Dirty(targetEntity, leashedComp);
        }

        return true;
    }

    /// <summary>
    /// Removing the leash
    /// </summary>
    public override bool TryDetachLeash(EntityUid leashUid, LeashComponent? leash = null, EntityUid? user = null)
    {
        if (!Resolve(leashUid, ref leash))
            return false;

        var target = leash.AttachedEntity;

        // Removing the connection
        if (leash.JointId != null && target != null)
        {
            _jointSystem.RemoveJoint(target.Value, leash.JointId);
        }

        if (LifeStage(leashUid) < EntityLifeStage.Terminating)
        {
            Dirty(leashUid, leash);
        }

        return base.TryDetachLeash(leashUid, leash, user);
    }
}
