// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared.Body.Events;
using Content.Shared.Damage.Events;
using Content.Shared.Electrocution;
using Content.Goobstation.Common.Stunnable;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

public sealed partial class BiowareSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    [SubscribeLocalEvent]
    private void OnStep(Entity<SurgeryBiowareComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer)
            return;

        var source = Prototype(ent)!.ID;
        switch (ent.Comp.Category)
        {
            case BiowareCategory.Blood:
                var blood = EnsureComp<BloodBiowareComponent>(args.Body);
                blood.Source = source;
                blood.BleedMultiplier = ent.Comp.BleedMultiplier;
                Dirty(args.Body, blood);
                break;
            case BiowareCategory.Nerves:
                var nerves = EnsureComp<NerveBiowareComponent>(args.Body);
                nerves.Source = source;
                nerves.StunMultiplier = ent.Comp.StunMultiplier;
                nerves.StaminaMultiplier = ent.Comp.StaminaMultiplier;
                nerves.ShockImmune = ent.Comp.ShockImmune;
                Dirty(args.Body, nerves);
                break;
        }

        args.Complete = true;
    }

    [SubscribeLocalEvent]
    private void OnCompleteCheck(Entity<SurgeryBiowareComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        var source = new EntProtoId(Prototype(ent)!.ID);
        var complete = ent.Comp.Category switch
        {
            BiowareCategory.Blood => TryComp<BloodBiowareComponent>(args.Body, out var blood) && blood.Source == source,
            BiowareCategory.Nerves => TryComp<NerveBiowareComponent>(args.Body, out var nerves) && nerves.Source == source,
            _ => false,
        };
        if (!complete)
            args.Cancelled = true;
    }

    public float GetStunMultiplier(EntityUid body)
    {
        return TryComp<NerveBiowareComponent>(body, out var nerves) ? nerves.StunMultiplier : 1f;
    }

    [SubscribeLocalEvent]
    private void OnBleed(Entity<BloodBiowareComponent> ent, ref BleedModifierEvent args)
    {
        args.BleedAmount *= ent.Comp.BleedMultiplier;
    }

    [SubscribeLocalEvent]
    private void OnStunModifier(Entity<NerveBiowareComponent> ent, ref GetClothingStunModifierEvent args)
    {
        args.Modifier *= ent.Comp.StunMultiplier;
    }

    [SubscribeLocalEvent]
    private void OnStaminaDamage(Entity<NerveBiowareComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        args.Value *= ent.Comp.StaminaMultiplier;
    }

    [SubscribeLocalEvent]
    private void OnElectrocutionAttempt(Entity<NerveBiowareComponent> ent, ref ElectrocutionAttemptEvent args)
    {
        if (ent.Comp.ShockImmune)
        {
            args.SiemensCoefficient *= ent.Comp.GroundingSiemensCoefficient;
            args.Grounded = true;
        }
    }
}
