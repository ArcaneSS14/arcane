using Content.Shared._Arcane.Weapons.Aim;
using Content.Shared.Camera;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using System.Collections.Generic;
using System.Numerics;

namespace Content.Client._Arcane.Weapons.Aim;
public sealed class ArcaneAimCameraOffsetSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedContentEyeSystem _eye = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _shuttingDown = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArcaneAimCameraOffsetComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ArcaneAimCameraOffsetComponent, GetEyeOffsetEvent>(OnGetEyeOffset);
    }

    private void OnShutdown(Entity<ArcaneAimCameraOffsetComponent> ent, ref ComponentShutdown args)
    {
        _shuttingDown.Add(ent.Owner);

        if (TryComp<EyeComponent>(ent.Owner, out var eye))
            _eye.UpdateEyeOffset((ent.Owner, eye));

        _shuttingDown.Remove(ent.Owner);
    }

    private void OnGetEyeOffset(
        Entity<ArcaneAimCameraOffsetComponent> ent,
        ref GetEyeOffsetEvent args)
    {
        if (_player.LocalEntity != ent.Owner || _shuttingDown.Contains(ent.Owner))
            return;

        var rotation = _transform.GetWorldRotation(Transform(ent.Owner));
        var facing = rotation.RotateVec(Vector2.UnitY);
        args.Offset -= facing * ent.Comp.Offset;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_player.LocalEntity is not { } player ||
            !HasComp<ArcaneAimCameraOffsetComponent>(player) ||
            !TryComp<EyeComponent>(player, out var eye))
        {
            return;
        }

        _eye.UpdateEyeOffset((player, eye));
    }
}
