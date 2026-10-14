using Content.Shared._Arcane.Weapons.TargetLock;
using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using System.Numerics;

namespace Content.Server._Arcane.Weapons;

public sealed class TargetLockSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TargetLockComponent, ArcaneTargetLockSelectEvent>(OnSelectAction);
        SubscribeLocalEvent<TargetLockComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<TargetLockComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<TargetLockComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnSelectAction(Entity<TargetLockComponent> ent, ref ArcaneTargetLockSelectEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        EntityUid user = args.Performer;
        EntityUid target = args.Target;
        ent.Comp.User = user;
        ent.Comp.LockedTarget = null;
        Dirty(ent);

        if (TerminatingOrDeleted(target) || !_mobState.IsAlive(target))
        {
            _popup.PopupEntity(Loc.GetString("arcane-target-lock-none"), ent.Owner, user);
            return;
        }

        var userCoords = _xform.GetMapCoordinates(user);
        var targetCoords = _xform.GetMapCoordinates(target);

        if (userCoords.MapId != targetCoords.MapId)
        {
            _popup.PopupEntity(Loc.GetString("arcane-target-lock-none"), ent.Owner, user);
            return;
        }

        if ((userCoords.Position - targetCoords.Position).Length() > ent.Comp.Range)
        {
            _popup.PopupEntity(Loc.GetString("arcane-target-lock-out-of-range"), ent.Owner, user);
            return;
        }

        ent.Comp.LockedTarget = target;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("arcane-target-lock-enabled",
            ("target", Name(target))), ent.Owner, user);
    }

    private void OnAmmoShot(Entity<TargetLockComponent> ent, ref AmmoShotEvent args)
    {
        if (ent.Comp.LockedTarget is not { } target)
            return;

        if (TerminatingOrDeleted(target) || !_mobState.IsAlive(target))
        {
            ent.Comp.LockedTarget = null;
            Dirty(ent);
            return;
        }

        var targetCoords = _xform.GetMapCoordinates(target);

        foreach (EntityUid projectile in args.FiredProjectiles)
        {
            if (!TryComp<ProjectileComponent>(projectile, out _))
                continue;

            if (HasComp<HomingProjectileComponent>(projectile))
                continue;

            if (!TryComp<PhysicsComponent>(projectile, out var physics))
                continue;

            var projCoords = _xform.GetMapCoordinates(projectile);
            if (projCoords.MapId != targetCoords.MapId)
                continue;

            Vector2 dirToTarget = targetCoords.Position - projCoords.Position;
            if (dirToTarget.LengthSquared() <= 0.0001f)
                continue;

            float speed = physics.LinearVelocity.Length();
            if (speed < 0.01f)
                speed = 30f;

            Vector2 newVelocity = Vector2.Normalize(dirToTarget) * speed;
            _physics.SetLinearVelocity(projectile, newVelocity, body: physics);
        }

        ent.Comp.LockedTarget = null;
        Dirty(ent);
    }

    private void OnUnequipped(Entity<TargetLockComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (ent.Comp.LockedTarget == null)
            return;

        ent.Comp.LockedTarget = null;
        Dirty(ent);
    }

    private void OnTerminating(Entity<TargetLockComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.LockedTarget == null)
            return;

        ent.Comp.LockedTarget = null;
    }
}
