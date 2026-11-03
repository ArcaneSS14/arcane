using Content.Shared._Arcane.Surgery.Tools;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._Arcane.Surgery;

public sealed class SharedAutopsySystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SurgeryToolExamineSystem _toolExamine = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryAutopsyComponent, SurgeryValidEvent>(OnValid);
        SubscribeLocalEvent<SurgeryAutopsyComponent, SurgeryStepCompleteCheckEvent>(OnCompleteCheck);
        SubscribeLocalEvent<SurgeryAnalyzerComponent, SurgeryToolExaminedEvent>(_toolExamine.OnExamined);
    }

    private void OnValid(Entity<SurgeryAutopsyComponent> ent, ref SurgeryValidEvent args)
    {
        if (!_mobState.IsDead(args.Body) || HasComp<SurgicallyAnalyzedComponent>(args.Body))
            args.Cancelled = true;
    }

    private void OnCompleteCheck(Entity<SurgeryAutopsyComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!HasComp<SurgicallyAnalyzedComponent>(args.Body))
            args.Cancelled = true;
    }
}
