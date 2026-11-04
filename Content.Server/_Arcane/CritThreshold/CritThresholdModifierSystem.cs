using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Arcane.CritThreshold;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;

namespace Content.Server._Arcane.CritThreshold;

/// <summary>
///     Scales crit and death thresholds by everything that answers <see cref="RefreshCritThresholdEvent"/>.
/// </summary>
public sealed class CritThresholdModifierSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    private static readonly MobState[] ScaledStates = [MobState.SoftCritical, MobState.HardCritical, MobState.Dead];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CritThresholdModifierStatusEffectComponent, StatusEffectAppliedEvent>(OnEffectApplied);
        SubscribeLocalEvent<CritThresholdModifierStatusEffectComponent, StatusEffectRemovedEvent>(OnEffectRemoved);
        SubscribeLocalEvent<CritThresholdModifierStatusEffectComponent, StatusEffectRelayedEvent<RefreshCritThresholdEvent>>(OnRefreshCritThreshold);
    }

    private void OnEffectApplied(Entity<CritThresholdModifierStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        RefreshCritThreshold(args.Target);
    }

    private void OnEffectRemoved(Entity<CritThresholdModifierStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RefreshCritThreshold(args.Target);
    }

    private void OnRefreshCritThreshold(Entity<CritThresholdModifierStatusEffectComponent> ent, ref StatusEffectRelayedEvent<RefreshCritThresholdEvent> args)
    {
        var evArgs = args.Args;
        evArgs.Raise = MathF.Max(evArgs.Raise, ent.Comp.Modifier);
        evArgs.Drop = MathF.Min(evArgs.Drop, ent.Comp.Modifier);
        args.Args = evArgs;
    }

    public void RefreshCritThreshold(Entity<MobThresholdsComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var ev = new RefreshCritThresholdEvent();
        RaiseLocalEvent(ent, ref ev);
        var modifier = ev.Raise * ev.Drop * ev.Modifier;

        // Thresholds are cached only while scaled, so what other systems set in between is kept.
        if (!TryComp<CritThresholdModifiedComponent>(ent, out var modified))
        {
            if (modifier == 1f)
                return;

            modified = AddComp<CritThresholdModifiedComponent>(ent);
            foreach (var state in ScaledStates)
            {
                if (_mobThreshold.TryGetThresholdForState(ent, state, out var threshold, ent.Comp))
                    modified.Baseline[state] = threshold.Value;
            }
        }

        // Raising goes from death down and lowering from crit up, so a mob never passes through death on the way.
        var states = ScaledStates.Where(modified.Baseline.ContainsKey).ToList();
        if (states.Count > 0
            && _mobThreshold.TryGetThresholdForState(ent, states[0], out var current, ent.Comp)
            && modified.Baseline[states[0]].Float() * modifier > current.Value.Float())
            states.Reverse();

        foreach (var state in states)
        {
            _mobThreshold.SetMobStateThreshold(ent, FixedPoint2.New(modified.Baseline[state].Float() * modifier), state, ent.Comp);
        }

        if (modifier == 1f)
            RemComp<CritThresholdModifiedComponent>(ent);
    }
}
