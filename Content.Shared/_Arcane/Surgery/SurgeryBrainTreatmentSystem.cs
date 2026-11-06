using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Traits;
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

        foreach (var organ in _body.GetPartOrgans(args.Part))
        {
            if (!HasComp<BrainComponent>(organ.Id))
                continue;

            var remaining = organ.Component.IntegrityCap * ent.Comp.HealFraction;
            foreach (var (key, damage) in organ.Component.IntegrityModifiers.ToList())
            {
                if (remaining <= 0)
                    break;
                if (damage <= 0)
                    continue;

                var healed = FixedPoint2.Min(remaining, damage);
                _trauma.TryChangeOrganDamageModifier(organ.Id, -healed, key.Item2, key.Item1, organ.Component);
                remaining -= healed;
            }
        }
    }

    private void OnBotched(Entity<SurgeryBrainTreatmentComponent> ent, ref SurgeryStepBotchedEvent args)
    {
        if (!_net.IsServer)
            return;

        foreach (var organ in _body.GetPartOrgans(args.Part))
        {
            if (!HasComp<BrainComponent>(organ.Id))
                continue;

            var damage = organ.Component.IntegrityCap * ent.Comp.FailureDamageFraction;
            if (!_trauma.TryChangeOrganDamageModifier(organ.Id, damage, organ.Id, nameof(SurgeryBrainTreatmentSystem), organ.Component))
                _trauma.TryCreateOrganDamageModifier(organ.Id, damage, organ.Id, nameof(SurgeryBrainTreatmentSystem), organ.Component);
        }

        EntityManager.AddComponents(args.Body, _prototypes.Index(ent.Comp.FailureTrait).Components, false);
    }

    private void OnCompleteCheck(Entity<SurgeryBrainTreatmentComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (_body.GetPartOrgans(args.Part).Any(o => HasComp<BrainComponent>(o.Id)
            && o.Component.IntegrityModifiers.Values.Any(d => d > 0)))
            args.Cancelled = true;
    }
}
