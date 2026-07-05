using System.Linq;
using Content.Server.NPC.HTN;
using Content.Shared._Arcane.CCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.NpcSleep;

public sealed partial class NpcSleepSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private int _sleepRange;
    private static TimeSpan _nextUpdate = TimeSpan.MinValue;
    private static TimeSpan _updateInterval = TimeSpan.FromSeconds(1);

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
            ProccessNpc();
    }

    private void ProccessNpc()
    {
        _nextUpdate = _timing.CurTime + _updateInterval;

        var query = EntityQueryEnumerator<HTNComponent, TransformComponent>();

        while (query.MoveNext(out _, out var htn, out var transform))
        {
            if (!_lookup.GetEntitiesInRange<ActorComponent>(transform.Coordinates, _sleepRange).Any())
            {
                htn.Enabled = false;
                continue;
            }

            htn.Enabled = true;
        }
    }
}
