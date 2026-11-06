using Content.Server.Research.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Arcane.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.NPC.Systems;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Station;
using Content.Shared.Whitelist;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.Surgery;

/// <summary>
///     Surgery tied to the research server of the surgeon's own network: technology-gated surgeries, autopsies and dissections.
/// </summary>
public sealed class SurgeryResearchSystem : SharedSurgeryResearchSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private static readonly TimeSpan AccessUpdateInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextAccessUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<SurgeryAutopsyComponent, SurgeryStepEvent>(OnAutopsy);
        SubscribeLocalEvent<SurgeryDissectionComponent, SurgeryStepEvent>(OnDissection);
        SubscribeLocalEvent<SurgeryDissectionComponent, SurgeryStepBotchedEvent>(OnDissectionBotched);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextAccessUpdate)
            return;

        _nextAccessUpdate = _timing.CurTime + AccessUpdateInterval;

        // Research and job changes raise nothing on the surgeon, so their clients are kept current here.
        var query = EntityQueryEnumerator<SurgeryResearchAccessComponent>();
        while (query.MoveNext(out var uid, out var access))
        {
            UpdateAccess((uid, access));
        }
    }

    protected override IReadOnlyCollection<ProtoId<TechnologyPrototype>> GetResearchedTechnologies(EntityUid user)
    {
        return TryGetSurgeonServer(user, out var server) ? server.Comp2.ResearchedTechnologies : [];
    }

    /// <summary>
    /// The research server of the surgeon's network: the crew use their station's, others their faction's own.
    /// </summary>
    public bool TryGetSurgeonServer(EntityUid surgeon, out Entity<ResearchServerComponent, TechnologyDatabaseComponent> server)
    {
        server = default;
        Entity<ResearchServerComponent, TechnologyDatabaseComponent>? crewServer = null;
        Entity<ResearchServerComponent, TechnologyDatabaseComponent>? factionServer = null;
        var query = EntityQueryEnumerator<SurgeryResearchServerComponent, ResearchServerComponent, TechnologyDatabaseComponent>();
        while (query.MoveNext(out var uid, out var access, out var research, out var database))
        {
            if (!_faction.IsMember(surgeon, access.Faction))
                continue;

            if (!access.StationCrew)
                Pick(ref factionServer, (uid, research, database));
            else if (IsStationCrew(surgeon, _station.GetOwningStation(uid)))
                Pick(ref crewServer, (uid, research, database));
        }

        // A recruited crew member keeps the station's research instead of taking the new faction's.
        if ((crewServer ?? factionServer) is not { } found)
            return false;

        server = found;
        return true;
    }

    // The oldest server is the authority of its network, which holds the network's technologies and points.
    private static void Pick(ref Entity<ResearchServerComponent, TechnologyDatabaseComponent>? current,
        Entity<ResearchServerComponent, TechnologyDatabaseComponent> candidate)
    {
        if (current == null || candidate.Comp1.Id < current.Value.Comp1.Id)
            current = candidate;
    }

    private bool IsStationCrew(EntityUid surgeon, EntityUid? station)
    {
        return station != null
            && _mind.TryGetMind(surgeon, out _, out var mind)
            && mind.UserId is { } userId
            && _stationJobs.TryGetPlayerJobs(station.Value, userId, out _);
    }

    private void UpdateAccess(Entity<SurgeryResearchAccessComponent> surgeon)
    {
        var researched = GetResearchedTechnologies(surgeon);
        if (surgeon.Comp.Technologies.SetEquals(researched))
            return;

        surgeon.Comp.Technologies = new HashSet<ProtoId<TechnologyPrototype>>(researched);
        Dirty(surgeon);
    }

    // Clients predict surgery for the body they control, so they need its research before the first click.
    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        // Ghosts and other bodies without hands cannot operate.
        if (!HasComp<HandsComponent>(args.Entity))
            return;

        UpdateAccess((args.Entity, EnsureComp<SurgeryResearchAccessComponent>(args.Entity)));
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        RemCompDeferred<SurgeryResearchAccessComponent>(args.Entity);
    }

    private void OnAutopsy(Entity<SurgeryAutopsyComponent> ent, ref SurgeryStepEvent args)
    {
        if (GetBodyKind(args.Body, ent.Comp.Experiments) is { } experiment && TryGetSurgeonServer(args.User, out var server))
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

        if (GetBodyKind(body, ent.Comp.Rewards) is { } reward && TryGetSurgeonServer(user, out var server))
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
}
