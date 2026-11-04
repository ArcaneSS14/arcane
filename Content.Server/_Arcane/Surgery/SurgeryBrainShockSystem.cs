// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Medical;
using Content.Shared._Arcane.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Arcane.Surgery;

public sealed class SurgeryBrainShockSystem : EntitySystem
{
    private const string BrainDamageId = "SurgicalRevival";

    [Dependency] private readonly DefibrillatorSystem _defibrillator = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly TraumaSystem _trauma = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryRevivalStepComponent, SurgeryStepEvent>(OnStep);
    }

    private void OnStep(Entity<SurgeryRevivalStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryComp<DefibrillatorComponent>(args.Tool, out var defib))
            return;

        if (_defibrillator.Zap(args.Tool, args.Body, args.User, defib, ignoreRot: true) != args.Body)
            return;

        var revived = !_mobState.IsDead(args.Body);
        DamageBrain(args.Body,
            revived ? ent.Comp.SuccessBrainDamage : ent.Comp.FailureBrainDamage,
            revived ? ent.Comp.SuccessIntegrityFloor : ent.Comp.FailureIntegrityFloor);
    }

    // Like tgstation, the shock hurts the brain but never destroys it outright.
    private void DamageBrain(EntityUid body, FixedPoint2 damage, FixedPoint2 floor)
    {
        foreach (var brain in _body.GetBodyOrganEntityComps<BrainComponent>(body))
        {
            var amount = FixedPoint2.Min(damage, brain.Comp2.OrganIntegrity - floor);
            if (amount <= 0)
                continue;

            if (!_trauma.TryChangeOrganDamageModifier(brain, amount, body, BrainDamageId, brain.Comp2))
                _trauma.TryCreateOrganDamageModifier(brain, amount, body, BrainDamageId, brain.Comp2);
        }
    }
}
