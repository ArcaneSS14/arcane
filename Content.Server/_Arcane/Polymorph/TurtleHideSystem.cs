using Content.Shared._Arcane.Polymorph;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Components;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.Polymorph;

public sealed class TurtleHideSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobTurtleComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(EntityUid uid, MobTurtleComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null || args.DamageDelta.GetTotal() <= 0)
            return;

        if (component.IsHidden)
            return;

        component.IsHidden = true;
        _appearance.SetData(uid, AnimalVisuals.TurtleHide, true);

        if (TryComp<InputMoverComponent>(uid, out var mover))
        {
            mover.CanMove = false;
            Dirty(uid, mover);
        }

        Timer.Spawn(TimeSpan.FromSeconds(5), () =>
        {
            if (!Exists(uid)) return;

            component.IsHidden = false;
            _appearance.SetData(uid, AnimalVisuals.TurtleHide, false);
            if (TryComp<InputMoverComponent>(uid, out var mover))
            {
                mover.CanMove = true;
                Dirty(uid, mover);
            }
        });
    }
}
