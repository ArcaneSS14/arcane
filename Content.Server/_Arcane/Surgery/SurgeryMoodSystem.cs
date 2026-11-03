using Content.Server._Orion.Mood;
using Content.Shared._Arcane.Surgery;
using Content.Shared._Orion.Mood;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Components;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Drunk;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.Surgery;

public sealed class SurgeryMoodSystem : EntitySystem
{
    [Dependency] private readonly ConsciousnessSystem _consciousness = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MoodComponent, SurgeryMoodEvent>(OnSurgeryMood);
    }

    private void OnSurgeryMood(Entity<MoodComponent> ent, ref SurgeryMoodEvent args)
    {
        var suppressed = !_mobState.IsAlive(ent)
            || TryComp<ConsciousnessComponent>(ent, out var consciousness) && !consciousness.IsConscious
            || HasComp<SleepingComponent>(ent)
            || _status.HasEffectComp<ForcedSleepingStatusEffectComponent>(ent)
            || _status.HasEffectComp<DrunkStatusEffectComponent>(ent);

        if (_consciousness.TryGetNerveSystem(ent, out var nerves))
        {
            foreach (var (key, modifier) in nerves.Value.Comp.Modifiers)
            {
                if (key.Item2 == "PainSuppressant" && modifier.Change < 0
                    && (modifier.Time == null || modifier.Time > _timing.CurTime))
                {
                    suppressed = true;
                    break;
                }
            }
        }

        if (suppressed)
        {
            if (ent.Comp.CategorisedEffects.TryGetValue("Surgery", out var current))
                RaiseLocalEvent(ent.Owner, new MoodRemoveEffectEvent(current));
            return;
        }

        ProtoId<MoodEffectPrototype> moodlet = args.Outcome switch
        {
            SurgeryMoodOutcome.Started => "SurgeryStarted",
            SurgeryMoodOutcome.Success => "SurgerySuccess",
            SurgeryMoodOutcome.Failure => "SurgeryFailure",
            _ => throw new ArgumentOutOfRangeException(),
        };
        RaiseLocalEvent(ent.Owner, new MoodEffectEvent(moodlet));
    }
}
