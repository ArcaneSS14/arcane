// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Goobstation.Shared.EntityEffects.EffectConditions;

public sealed partial class UniqueBloodstreamChemThresholdSystem : EntityConditionSystem<BloodstreamComponent, UniqueBloodstreamChemThreshold>
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    protected override void Condition(Entity<BloodstreamComponent> entity, ref EntityConditionEvent<UniqueBloodstreamChemThreshold> args)
    {
        if (_solution.ResolveSolution(entity.Owner, entity.Comp.BloodSolutionName, ref entity.Comp.BloodSolution, out var chemSolution))
        {
            // Arcane-Edit-Start
            var bloodReferenceSolution = entity.Comp.BloodReferenceSolution;

            var chemicalCount = chemSolution.Contents.Count(quant =>
                !bloodReferenceSolution.ContainsPrototype(quant.Reagent.Prototype));

            args.Result = chemicalCount > args.Condition.Min &&
                chemicalCount < args.Condition.Max;
            // Arcane-Edit-End
            return;
        }
        args.Result = false;
    }
}

public sealed partial class UniqueBloodstreamChemThreshold : EntityConditionBase<UniqueBloodstreamChemThreshold>
{
    [DataField]
    public int Max = int.MaxValue;

    [DataField]
    public int Min = -1;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        return Loc.GetString("reagent-effect-condition-guidebook-unique-bloodstream-chem-threshold",
            ("max", Max),
            ("min", Min));
    }
}
