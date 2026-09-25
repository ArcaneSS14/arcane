using Content.Server.Hands.Systems;
using Content.Shared._Arcane.Faoli.Components;
using Content.Shared.Alert;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Timing;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage;
using Content.Server.Administration;
using Content.Shared._Arcane.Faoli;

namespace Content.Server._Arcane.Faoli;

public sealed partial class FaoliSystem : EntitySystem
{
    [Dependency] private readonly SharedFaoliSystem _faoli = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FaoliComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FaoliComponent, DamageChangedEvent>(OnDamageChanged);

        SubscribeAbilities();
    }

    private void OnInit(Entity<FaoliComponent> ent, ref ComponentInit args)
    {
        _alerts.ShowAlert(ent.Owner, ent.Comp.FaoliAlert);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FaoliComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_gameTiming.CurTime < comp.NextTickTime)
                continue;

            if (comp.Faoli < comp.Low) // 15
            {
                comp.NextTickTime = _gameTiming.CurTime + TimeSpan.FromSeconds(comp.LowInterval);
                _faoli.TryChangeFaoliAmount(uid, comp.Regeneartion, comp);
                continue;
            }

            if (comp.Faoli < comp.Maximum) // 50
            {
                comp.NextTickTime = _gameTiming.CurTime + TimeSpan.FromSeconds(comp.Interval);
                _faoli.TryChangeFaoliAmount(uid, comp.Regeneartion, comp);
                continue;
            }

            if (comp.Faoli < comp.Limit) // 100
            {
                comp.NextTickTime = _gameTiming.CurTime + TimeSpan.FromSeconds(comp.Interval);
                continue;
            }

            if (comp.Faoli <= comp.OverflowLimit) // 150
            {
                comp.NextTickTime = _gameTiming.CurTime + TimeSpan.FromSeconds(comp.OverflowInterval);
                _faoli.TryChangeFaoliAmount(uid, -comp.Regeneartion, comp);
                continue;
            }

            continue;
        }
    }

    private void OnDamageChanged(Entity<FaoliComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta is null || !args.DamageIncreased)
        {
            return;
        }

        if (args.DamageDelta.DamageDict.TryGetValue(ent.Comp.FaoliDamageType, out FixedPoint2 value))
        {
            _faoli.TryChangeFaoliAmount(ent.Owner, -value * ent.Comp.FaoliDamageMultiplier, ent.Comp);
        }

        return;
    }

    public bool OnUseAbility(EntityUid uid, FixedPoint2 cost)
    {
        if (cost > 0)
        {
            if (!TryComp<FaoliComponent>(uid, out var comp))
                return false;

            if (comp.Faoli >= cost)
            {
                _faoli.TryChangeFaoliAmount(uid, -cost, comp);
                return true;
            }

            _popup.PopupEntity(Loc.GetString("faoli-not-enough"), uid, uid);
            return false;
        }

        return true;
    }

    public bool TryTransferFaoli(EntityUid performer, EntityUid target, FixedPoint2 amount, bool popup = true)
    {
        if (TryComp<FaoliComponent>(performer, out var comp) && TryComp<FaoliComponent>(target, out var targetComp))
        {
            if (comp.Faoli >= amount)
            {
                _faoli.TryChangeFaoliAmount(performer, -amount, comp);
                _faoli.TryChangeFaoliAmount(target, amount, targetComp);
                return true;
            }

            if (popup)
                _popup.PopupEntity(Loc.GetString("faoli-not-enough"), performer, performer);

            return false;
        }
        if (popup)
            _popup.PopupEntity(Loc.GetString("faoli-target-has-no-faoli"), performer, performer);

        return false;
    }
}
