using Content.Shared._Arcane.Weapons.Aim;
using Content.Shared.Camera;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;

namespace Content.Server._Arcane.Weapons.Aim;

public sealed class ArcaneAimSystem : SharedArcaneAimSystem
{
    [Dependency] private readonly SharedContentEyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArcaneAimPvsComponent, GetEyePvsScaleEvent>(OnGetPvsScale);
    }

    private void OnGetPvsScale(Entity<ArcaneAimPvsComponent> ent, ref GetEyePvsScaleEvent args)
    {
        args.Scale += ent.Comp.Increase;
    }

    protected override void EnableAim(Entity<ArcaneAimComponent> ent, EntityUid user)
    {
        base.EnableAim(ent, user);

        var pvs = EnsureComp<ArcaneAimPvsComponent>(user);
        pvs.Increase = ent.Comp.PvsIncrease;

        if (TryComp<ContentEyeComponent>(user, out var contentEye) &&
            TryComp<EyeComponent>(user, out var eye))
        {
            _eye.UpdatePvsScale(user, contentEye, eye);
        }
    }

    protected override void DisableAim(Entity<ArcaneAimComponent> ent, EntityUid user)
    {
        var wasSource = TryComp<ArcaneEyeCursorOffsetComponent>(user, out var offset) &&
                        offset.Source == ent.Owner;

        base.DisableAim(ent, user);

        if (!wasSource)
            return;

        RemComp<ArcaneAimPvsComponent>(user);

        if (TryComp<ContentEyeComponent>(user, out var contentEye) &&
            TryComp<EyeComponent>(user, out var eye))
        {
            _eye.UpdatePvsScale(user, contentEye, eye);
        }
    }
}
