using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

public sealed class SurgeryBrainTreatmentSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly TraumaSystem _trauma = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryBrainTreatmentComponent, SurgeryStepEvent>(OnStep);
        SubscribeLocalEvent<SurgeryBrainTreatmentComponent, SurgeryStepBotchedEvent>(OnBotched);
        SubscribeLocalEvent<SurgeryBrainTreatmentComponent, SurgeryStepCompleteCheckEvent>(OnCompleteCheck);
    }

    private void OnStep(Entity<SurgeryBrainTreatmentComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer)
            return;

        foreach (var brain in _body.GetBodyPartOrganComponents<BrainComponent>(args.Part))
        {
            var remaining = brain.Organ.IntegrityCap * ent.Comp.HealFraction;
            foreach (var (key, damage) in brain.Organ.IntegrityModifiers.ToList())
            {
                if (remaining <= 0)
                    break;
                if (damage <= 0)
                    continue;

                var healed = FixedPoint2.Min(remaining, damage);
                _trauma.TryChangeOrganDamageModifier(brain.Owner, -healed, key.Item2, key.Item1, brain.Organ);
                remaining -= healed;
            }
        }
    }

    private void OnBotched(Entity<SurgeryBrainTreatmentComponent> ent, ref SurgeryStepBotchedEvent args)
    {
        if (!_net.IsServer)
            return;

        foreach (var brain in _body.GetBodyPartOrganComponents<BrainComponent>(args.Part))
        {
            var damage = brain.Organ.IntegrityCap * ent.Comp.FailureDamageFraction;
            if (!_trauma.TryChangeOrganDamageModifier(brain.Owner, damage, brain.Owner, nameof(SurgeryBrainTreatmentSystem), brain.Organ))
                _trauma.TryCreateOrganDamageModifier(brain.Owner, damage, brain.Owner, nameof(SurgeryBrainTreatmentSystem), brain.Organ);
        }

        EntityManager.AddComponents(args.Body, _prototypes.Index(ent.Comp.FailureTrait).Components, false);
    }

    private void OnCompleteCheck(Entity<SurgeryBrainTreatmentComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        // Integrity modifiers are not networked, so the client reads the integrity they add up to.
        if (_body.GetBodyPartOrganComponents<BrainComponent>(args.Part).Any(b => b.Organ.OrganIntegrity < b.Organ.IntegrityCap))
            args.Cancelled = true;
    }
}
