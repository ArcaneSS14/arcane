using Content.Shared._Arcane.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared.Research.Components;
using Content.Shared.Research.Systems;
using Content.Shared.Station;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Surgery;

public sealed class SurgeryTechnologyConditionSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedResearchSystem _research = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryTechnologyConditionComponent, SurgeryValidEvent>(OnValid);
    }

    private void OnValid(Entity<SurgeryTechnologyConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (args.Cancelled)
            return;

        var researched = false;
        if (_station.GetOwningStation(args.Body) is { } station)
        {
            var query = EntityQueryEnumerator<ResearchServerComponent, TechnologyDatabaseComponent>();
            while (query.MoveNext(out var uid, out _, out var database))
            {
                if (_station.GetOwningStation(uid) != station
                    || !_research.IsTechnologyUnlocked(uid, _prototypes.Index(ent.Comp.Technology), database))
                    continue;

                researched = true;
                break;
            }
        }

        if (researched == ent.Comp.Inverse)
            args.Cancelled = true;
    }
}
