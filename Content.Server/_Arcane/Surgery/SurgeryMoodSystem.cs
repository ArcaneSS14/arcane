using Content.Server._Orion.Mood;
using Content.Shared._Arcane.Surgery;
using Content.Shared._Orion.Mood;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Arcane.Surgery;

public sealed class SurgeryMoodSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SurgeryConditionsSystem _surgeryConditions = default!;
    [Dependency] private readonly SharedSurgerySystem _surgery = default!;

    private const string MoodCategory = "Surgery";
    private const float MaxDrunkIgnorePercent = 90f;

    private static readonly ProtoId<MoodEffectPrototype> StartedMoodlet = "SurgeryStarted";
    private static readonly ProtoId<MoodEffectPrototype> SuccessMoodlet = "SurgerySuccess";
    private static readonly ProtoId<MoodEffectPrototype> FailureMoodlet = "SurgeryFailure";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MoodComponent, SurgeryMoodEvent>(OnSurgeryMood);
    }

    private void OnSurgeryMood(Entity<MoodComponent> ent, ref SurgeryMoodEvent args)
    {
        if (_surgery.GetSingleton(args.Step) is { } step && HasComp<SurgeryNoMoodStepComponent>(step))
            return;

        ent.Comp.CategorisedEffects.TryGetValue(MoodCategory, out var current);
        var drunkIgnoreChance = MathF.Min(_surgeryConditions.GetDrunkenness(ent), MaxDrunkIgnorePercent) / 100f;
        if (_status.HasEffectComp<AnalgesiaStatusEffectComponent>(ent) || _random.Prob(drunkIgnoreChance))
        {
            if (current != null)
                RaiseLocalEvent(ent.Owner, new MoodRemoveEffectEvent(current));
            return;
        }

        // A patient who fell asleep mid-surgery still wakes up to its outcome, but gets no new moodlet.
        if (!IsConscious(ent) && current != StartedMoodlet.Id)
            return;

        var moodlet = args.Outcome switch
        {
            SurgeryMoodOutcome.Started => StartedMoodlet,
            SurgeryMoodOutcome.Success => SuccessMoodlet,
            _ => FailureMoodlet,
        };
        RaiseLocalEvent(ent.Owner, new MoodEffectEvent(moodlet));
    }

    // Shitmed's consciousness can disagree with the mob state, since its UpdateMobState is disabled.
    private bool IsConscious(EntityUid patient)
    {
        return _mobState.IsAlive(patient)
            && (!TryComp<ConsciousnessComponent>(patient, out var consciousness) || consciousness.IsConscious)
            && !HasComp<SleepingComponent>(patient)
            && !_status.HasEffectComp<ForcedSleepingStatusEffectComponent>(patient);
    }
}
