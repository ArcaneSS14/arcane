using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._Arcane.Surgery;

public sealed class SharedDissectionSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryDissectionComponent, SurgeryValidEvent>(OnValid);
        SubscribeLocalEvent<SurgeryDissectionComponent, SurgeryStepCompleteCheckEvent>(OnCompleteCheck);
    }

    private void OnValid(Entity<SurgeryDissectionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!_mobState.IsDead(args.Body) || HasComp<SurgicallyDissectedComponent>(args.Body))
            args.Cancelled = true;
    }

    private void OnCompleteCheck(Entity<SurgeryDissectionComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!HasComp<SurgicallyDissectedComponent>(args.Body))
            args.Cancelled = true;
    }
}
