using Content.Server.NPC.HTN;
using Content.Shared._Arcane.CCVars;
using Content.Shared.Turrets;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.NpcSleep;

public sealed class NpcSleepSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly HTNSystem _htn = default!;

    private int _sleepRange;
    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(1);
    private readonly List<MapCoordinates> _players = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, ACCVars.NpcSleepRange, SetSleepRange, true);
    }

    private void SetSleepRange(int range)
    {
        _sleepRange = range;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_nextUpdate < _timing.CurTime)
            ProcessNpc();
    }

    private void ProcessNpc()
    {
        _nextUpdate = _timing.CurTime + _updateInterval;

        _players.Clear();
        var actors = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actors.MoveNext(out _, out var xform))
        {
            _players.Add(_transform.GetMapCoordinates(xform));
        }

        var query = EntityQueryEnumerator<HTNComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var htn, out var xform))
        {
            var sleeping = HasComp<NpcSleepingComponent>(uid);

            if (!PlayerNearby(_transform.GetMapCoordinates(xform)))
            {
                if (!htn.Enabled)
                    continue;

                _htn.SetHTNEnabled((uid, htn), false);
                EnsureComp<NpcSleepingComponent>(uid);
                continue;
            }

            if (!sleeping)
                continue;

            RemComp<NpcSleepingComponent>(uid);

            if (TryComp<DeployableTurretComponent>(uid, out var turret) && !turret.Enabled)
                continue;

            _htn.SetHTNEnabled((uid, htn), true);
        }
    }

    private bool PlayerNearby(MapCoordinates coords)
    {
        var rangeSquared = _sleepRange * _sleepRange;

        foreach (var player in _players)
        {
            if (player.MapId == coords.MapId && (player.Position - coords.Position).LengthSquared() <= rangeSquared)
                return true;
        }

        return false;
    }
}
