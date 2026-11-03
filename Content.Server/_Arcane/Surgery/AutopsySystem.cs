using Content.Server.Research.Systems;
using Content.Shared._Arcane.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._White.Xenomorphs.Xenomorph;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mobs.Systems;
using Content.Shared.Research.Components;
using Content.Shared.Station;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Surgery;

public sealed class AutopsySystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;

    private static readonly ProtoId<SpeciesPrototype> HumanSpecies = "Human";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryAutopsyComponent, SurgeryStepEvent>(OnAutopsy);
    }

    private void OnAutopsy(Entity<SurgeryAutopsyComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_mobState.IsDead(args.Body) || HasComp<SurgicallyAnalyzedComponent>(args.Body))
            return;

        EnsureComp<SurgicallyAnalyzedComponent>(args.Body);
        args.Complete = true;

        var action = HasComp<XenomorphComponent>(args.Body) ? "AutopsyXenomorph"
            : TryComp<HumanoidAppearanceComponent>(args.Body, out var appearance) && appearance.Species == HumanSpecies
                ? "AutopsyHuman" : "AutopsyNonhuman";

        if (_station.GetOwningStation(args.Body) is not { } station)
            return;

        var query = EntityQueryEnumerator<ResearchServerComponent, TechnologyDatabaseComponent>();
        while (query.MoveNext(out var uid, out var server, out var database))
        {
            if (_station.GetOwningStation(uid) != station)
                continue;

            _research.TryProgressExperimentsByAction(uid, action, database, server);
            break;
        }
    }
}
