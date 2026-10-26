using Content.Goobstation.Shared.Sprinting;
using Content.Shared._Arcane.HeelSlip;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.HeelSlip;

public sealed class HeelSlipServerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const float CheckInterval = 0.5f;

    private float _accumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < CheckInterval)
            return;

        var interval = _accumulator;
        _accumulator = 0f;

        var query = EntityQueryEnumerator<SprinterComponent, InventoryComponent>();
        while (query.MoveNext(out var wearer, out var sprinter, out _))
        {
            if (!sprinter.IsSprinting)
                continue;

            if (HasComp<HeelSlipStatusComponent>(wearer))
                continue;

            if (!_inventory.TryGetSlotEntity(wearer, "shoes", out var shoes))
                continue;

            if (!TryComp<HeelSlipComponent>(shoes, out var heel))
                continue;

            var chance = Math.Clamp(heel.SlipChance * interval, 0f, 1f);
            if (!_random.Prob(chance))
                continue;

            TrySlip(wearer, heel);
        }
    }

    private void TrySlip(EntityUid wearer, HeelSlipComponent heel)
    {
        var duration = TimeSpan.FromSeconds(
            _random.NextFloat(heel.SlowdownDurationMin, heel.SlowdownDurationMax));

        var status = EnsureComp<HeelSlipStatusComponent>(wearer);
        status.EndTime = _timing.CurTime + duration;
        status.SlowdownModifier = heel.SlowdownModifier;
        Dirty(wearer, status);

        _stun.TryKnockdown(wearer, heel.KnockdownTime, force: true);
        _speed.RefreshMovementSpeedModifiers(wearer);
        _popup.PopupEntity(Loc.GetString("heel-slip-popup"), wearer, wearer);

        var ev = new HeelSlipEvent(wearer);
        RaiseLocalEvent(wearer, ref ev);
    }
}
