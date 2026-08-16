using Content.Server.Anomaly;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.Anomaly;

public sealed class PeriodicAnomalySpawnerSystem : EntitySystem
{
    [Dependency] private AnomalySystem _anomaly = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PeriodicAnomalySpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<PeriodicAnomalySpawnerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextAttempt = _timing.CurTime + ent.Comp.Interval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<PeriodicAnomalySpawnerComponent, TransformComponent>();

        while (query.MoveNext(out _, out var spawner, out var transform))
        {
            if (now < spawner.NextAttempt)
                continue;

            spawner.NextAttempt = now + spawner.Interval;

            if (!transform.Anchored)
                continue;

            if (transform.GridUid is not { } grid || !_random.Prob(spawner.Chance))
                continue;

            _anomaly.SpawnOnRandomGridLocation(grid, spawner.AnomalySpawnerPrototype);
        }
    }
}
