using Content.Shared._Arcane.Faoli.Events;
using Content.Shared.Interaction.Components;
using Content.Shared._Goobstation.Wizard.ArcaneBarrage;
using Robust.Shared.Spawners;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Robust.Shared.Player;
using Content.Server.Prayer;
using Content.Shared.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared._Arcane.Faoli;
using Content.Shared._Arcane.Faoli.Components;

namespace Content.Server._Arcane.Faoli;

public sealed partial class FaoliSystem
{
    [Dependency] private readonly PrayerSystem _prayer = default!;
    [Dependency] private readonly TemperatureSystem _temp = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    private EntityQuery<ActorComponent> _actorQuery;

    private void SubscribeAbilities()
    {
        SubscribeLocalEvent<SpawnnItemInHandEvent>(OnSpawnInHandEvent);
        SubscribeLocalEvent<TransfetFaoliEvent>(OnFaoliTransferEvent);
        SubscribeLocalEvent<BurstRegenerationEvent>(OnBurstRegenerationEvent);
        SubscribeLocalEvent<DemonicMessageEvent>(OnDemonicMessageEvent);
        SubscribeLocalEvent<TouchChangeTemperatureEvent>(OnTouchChangeTemperatureEvent);
        SubscribeLocalEvent<TouchChangeEnergy>(OnTouchChangeEnergy);

        _actorQuery = GetEntityQuery<ActorComponent>();
    }

    private void OnSpawnInHandEvent(SpawnnItemInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!_faoli.TryCheckFaoliAmount(args.Performer, args.Cost))
        {
            args.Handled = true;
            return;
        }

        var ent = Spawn(args.Prototype, Transform(args.Performer).Coordinates);
        if (!_hands.TryPickupAnyHand(args.Performer, ent) && args.Force)
        {
            QueueDel(ent);
            _popup.PopupEntity(Loc.GetString("faoli-no-hands"), args.Performer, args.Performer);
            args.Handled = true;

            return;
        }

        if (args.Sound != null)
            _audio.PlayPvs(args.Sound, args.Performer);

        if (args.Unremovable)
            EnsureComp<UnremoveableComponent>(ent);

        if (args.DeleteOnDrop)
            EnsureComp<DeleteOnDropAttemptComponent>(ent);

        if (args.TimedDespawn > 0)
        {
            var timedDespawn = EnsureComp<TimedDespawnComponent>(ent);
            timedDespawn.Lifetime = args.TimedDespawn;
        }

        if (!OnUseAbility(args.Performer, args.Cost))
        {
            args.Handled = true;
            return;
        }

        args.Handled = true;
    }

    private void OnFaoliTransferEvent(TransfetFaoliEvent args)
    {
        if (args.Handled)
            return;

        if (args.Performer == args.Target)
        {
            args.Handled = true;
            return;
        }

        if (!_faoli.TryCheckFaoliAmount(args.Performer, args.Amount + args.Cost))
        {
            args.Handled = true;
            return;
        }

        if (!OnUseAbility(args.Performer, args.Cost))
        {
            args.Handled = true;
            return;
        }

        if (!TryTransferFaoli(args.Performer, args.Target, args.Amount))
        {
            args.Handled = true;
            return;
        }

        if (args.Sound != null)
            _audio.PlayPvs(args.Sound, args.Performer);



        args.Handled = true;
    }

    private void OnBurstRegenerationEvent(BurstRegenerationEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<PassiveDamageComponent>(args.Target, out var comp) || !TryComp<DamageableComponent>(args.Target, out var damage))
        {
            _popup.PopupEntity(Loc.GetString("faoli-regeneration-no-component"), args.Performer, args.Performer);
            args.Handled = true;
            return;
        }

        if (args.Limited &&
        comp.DamageCap != 0 &&
        damage.TotalDamage >= comp.DamageCap)
        {
            _popup.PopupEntity(Loc.GetString("faoli-regeneration-limited"), args.Performer, args.Performer);
            args.Handled = true;
            return;
        }

        if (!OnUseAbility(args.Performer, args.Cost))
        {
            args.Handled = true;
            return;
        }

        _damageable.TryChangeDamage(args.Target, comp.Damage * args.Multiply, true, false, damage, targetPart: TargetBodyPart.All, splitDamage: comp.SplitBehavior);

        if (args.Sound != null)
            _audio.PlayPvs(args.Sound, args.Performer);

        args.Handled = true;
    }

    private void OnDemonicMessageEvent(DemonicMessageEvent args)
    {
        if (args.Handled)
            return;

        if (!_actorQuery.TryComp(args.Performer, out var performer) || !_actorQuery.TryComp(args.Target, out var target))
        {
            args.Handled = true;
            return;
        }

        if (!_faoli.TryCheckFaoliAmount(args.Performer, args.Cost))
        {
            args.Handled = true;
            return;
        }

        _quickDialog.OpenDialog(performer.PlayerSession, Loc.GetString("faoli-message"), "Message", (string message) =>
        {
            if (OnUseAbility(args.Performer, args.Cost))
                _prayer.SendSubtleMessage(target.PlayerSession, target.PlayerSession, message, Loc.GetString("faoli-message-whisper"));
        });

        if (args.Sound != null)
            _audio.PlayPvs(args.Sound, args.Performer);

        args.Handled = true;
    }

    private void OnTouchChangeTemperatureEvent(TouchChangeTemperatureEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<TemperatureComponent>(args.Target, out var comp))
        {
            args.Handled = true;
            return;
        }

        if (!OnUseAbility(args.Performer, args.Cost))
        {
            args.Handled = true;
            return;
        }

        if (args.Sound != null)
            _audio.PlayPvs(args.Sound, args.Performer);

        _temp.ChangeHeat(args.Target, args.Heat, args.IgnoreResistance, comp);

        args.Handled = true;
    }

    private void OnTouchChangeEnergy(TouchChangeEnergy args)
    {
        if (args.Handled)
            return;

        if (!TryComp<BatteryComponent>(args.Target, out var battery))
        {
            args.Handled = true;
            return;
        }

        if (!OnUseAbility(args.Performer, args.Cost))
        {
            args.Handled = true;
            return;
        }

        if (args.Sound != null)
            _audio.PlayPvs(args.Sound, args.Performer);

        var charge = _battery.GetCharge((args.Target, battery));
        _battery.SetCharge((args.Target, battery), charge + args.Amount);

        args.Handled = true;
    }
}
