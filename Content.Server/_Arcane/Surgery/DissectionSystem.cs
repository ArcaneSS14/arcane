using Content.Server.Research.Systems;
using Content.Shared._Arcane.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._White.Xenomorphs.Larva;
using Content.Shared._White.Xenomorphs.Queen;
using Content.Shared._White.Xenomorphs.Xenomorph;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mobs.Systems;
using Content.Shared.Research.Components;
using Content.Shared.Station;
using Content.Shared.Zombies;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Surgery;

public sealed class DissectionSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;

    private static readonly Dictionary<ProtoId<SpeciesPrototype>, float> SpeciesMultipliers = new()
    {
        { "SlimePerson", 2f },
        { "Diona", 2f },
    };

    private static readonly ProtoId<SpeciesPrototype> MonkeySpecies = "Monkey";
    private static readonly ProtoId<SpeciesPrototype> AbductorSpecies = "Abductor";

    private static readonly DamageSpecifier DissectionDamage = new()
    {
        DamageDict = new() { { "Slash", 80 } },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryDissectionComponent, SurgeryStepEvent>(OnDissection);
        SubscribeLocalEvent<SurgeryDissectionComponent, SurgeryStepBotchedEvent>(OnBotched);
    }

    private void OnDissection(Entity<SurgeryDissectionComponent> ent, ref SurgeryStepEvent args)
    {
        if (TryDissect(args.User, args.Body, args.Part, ent, false))
            args.Complete = true;
    }

    private void OnBotched(Entity<SurgeryDissectionComponent> ent, ref SurgeryStepBotchedEvent args)
    {
        TryDissect(args.User, args.Body, args.Part, ent, true);
    }

    private bool TryDissect(EntityUid user, EntityUid body, EntityUid part, EntityUid step, bool botched)
    {
        if (!_mobState.IsDead(body) || HasComp<SurgicallyDissectedComponent>(body))
            return false;

        EnsureComp<SurgicallyDissectedComponent>(body);
        var points = (int) MathF.Round(500f * GetRewardMultiplier(body) * (botched ? 0.01f : 1f));
        var damage = new SurgeryStepDamageEvent(user, body, part, step, DissectionDamage, 1f);
        RaiseLocalEvent(body, ref damage);

        if (_station.GetOwningStation(body) is not { } station)
            return true;

        var query = EntityQueryEnumerator<ResearchServerComponent>();
        while (query.MoveNext(out var uid, out var server))
        {
            if (_station.GetOwningStation(uid) != station)
                continue;

            _research.ModifyServerPoints(uid, points, server);
            break;
        }

        return true;
    }

    private float GetRewardMultiplier(EntityUid body)
    {
        if (TryComp<HumanoidAppearanceComponent>(body, out var appearance))
        {
            if (appearance.Species == MonkeySpecies)
                return 0.2f;

            if (appearance.Species == AbductorSpecies)
                return 4f;

            if (HasComp<ZombieComponent>(body))
                return 3f;

            return SpeciesMultipliers.GetValueOrDefault(appearance.Species, 1f);
        }

        if (HasComp<XenomorphQueenComponent>(body))
            return 10f;

        return HasComp<XenomorphComponent>(body) && !HasComp<XenomorphLarvaComponent>(body) ? 5f : 1f / 6f;
    }
}
