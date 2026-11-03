using Content.Server.Research.Systems;
using Content.Shared._Arcane.Surgery;
using Content.Shared._Arcane.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared.Humanoid;
using Content.Shared.Research.Components;
using Content.Shared.Station;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Surgery;

/// <summary>
///     Surgery tied to the research server of the patient's station: technology-gated surgeries, autopsies and dissections.
/// </summary>
public sealed class SurgeryResearchSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryTechnologyConditionComponent, SurgeryValidEvent>(OnTechnologyValid);
        SubscribeLocalEvent<SurgeryAutopsyComponent, SurgeryStepEvent>(OnAutopsy);
        SubscribeLocalEvent<SurgeryDissectionComponent, SurgeryStepEvent>(OnDissection);
        SubscribeLocalEvent<SurgeryDissectionComponent, SurgeryStepBotchedEvent>(OnDissectionBotched);
    }

    private void OnTechnologyValid(Entity<SurgeryTechnologyConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (args.Cancelled)
            return;

        var researched = false;
        var replaced = false;
        if (TryGetStationServer(args.Body, out var server))
        {
            researched = _research.IsTechnologyUnlocked(server, _prototypes.Index(ent.Comp.Technology), server.Comp2);
            replaced = ent.Comp.ReplacedBy is { } replacement
                && _research.IsTechnologyUnlocked(server, _prototypes.Index(replacement), server.Comp2);
        }

        if (replaced || researched == ent.Comp.Inverse)
            args.Cancelled = true;
    }

    private void OnAutopsy(Entity<SurgeryAutopsyComponent> ent, ref SurgeryStepEvent args)
    {
        if (GetBodyKind(args.Body, ent.Comp.Experiments) is { } experiment && TryGetStationServer(args.Body, out var server))
            _research.TryProgressExperimentsByAction(server, experiment.Action, server.Comp2, server.Comp1);
    }

    private void OnDissection(Entity<SurgeryDissectionComponent> ent, ref SurgeryStepEvent args)
    {
        Dissect(ent, args.User, args.Body, args.Part, 1f);
    }

    // A botched dissection still uses the body up, for a fraction of the points.
    private void OnDissectionBotched(Entity<SurgeryDissectionComponent> ent, ref SurgeryStepBotchedEvent args)
    {
        EnsureComp<SurgicallyDissectedComponent>(args.Body);
        Dissect(ent, args.User, args.Body, args.Part, ent.Comp.BotchMultiplier);
    }

    private void Dissect(Entity<SurgeryDissectionComponent> ent, EntityUid user, EntityUid body, EntityUid part, float multiplier)
    {
        var damage = new SurgeryStepDamageEvent(user, body, part, ent, ent.Comp.Damage, 1f);
        RaiseLocalEvent(body, ref damage);

        if (GetBodyKind(body, ent.Comp.Rewards) is { } reward && TryGetStationServer(body, out var server))
            _research.ModifyServerPoints(server, (int) MathF.Round(reward.Points * multiplier), server.Comp1);
    }

    private T? GetBodyKind<T>(EntityUid body, List<T> kinds) where T : SurgeryBodyKind
    {
        TryComp<HumanoidAppearanceComponent>(body, out var humanoid);
        foreach (var kind in kinds)
        {
            if (kind.Species.Count > 0 && (humanoid == null || !kind.Species.Contains(humanoid.Species)))
                continue;

            if (_whitelist.IsWhitelistPassOrNull(kind.Whitelist, body))
                return kind;
        }

        return null;
    }

    private bool TryGetStationServer(EntityUid body, out Entity<ResearchServerComponent, TechnologyDatabaseComponent> server)
    {
        server = default;
        if (_station.GetOwningStation(body) is not { } station)
            return false;

        var query = EntityQueryEnumerator<ResearchServerComponent, TechnologyDatabaseComponent>();
        while (query.MoveNext(out var uid, out var serverComp, out var database))
        {
            if (_station.GetOwningStation(uid) != station)
                continue;

            server = (uid, serverComp, database);
            return true;
        }

        return false;
    }
}
