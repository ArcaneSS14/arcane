using Content.Shared.Movement.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Arcane.HeelSlip;

public sealed class HeelSlipSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeelSlipStatusComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<HeelSlipStatusComponent>();
        while (query.MoveNext(out var uid, out var status))
        {
            if (_timing.CurTime < status.EndTime)
                continue;

            RemComp<HeelSlipStatusComponent>(uid);
            _speed.RefreshMovementSpeedModifiers(uid);
        }
    }

    private void OnRefreshMovespeed(
        EntityUid uid,
        HeelSlipStatusComponent status,
        RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(status.SlowdownModifier, status.SlowdownModifier);
    }
}
