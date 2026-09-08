using System.Collections.Generic;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.EntityEffects.Effects;

/// <summary>
/// Heals Airloss damage group and deals proportional toxic byproducts
/// based on actual healing done. When not overdosed, healing is capped to
/// current damage + buffer, ensuring a minimum tox even with no airloss damage.
/// Overdose removes the cap.
/// </summary>
public sealed partial class ChemConvermol : EntityEffectBase<ChemConvermol>
{
    [DataField]
    public ProtoId<DamageGroupPrototype> HealDamageGroup = "Airloss";

    [DataField]
    public ProtoId<DamageTypePrototype> ToxDamageType = "Poison";

    [DataField]
    public float HealPerTick = 1f;

    [DataField]
    public float Buffer = 0.5f;

    [DataField]
    public float ToxRatio = 5f;

    [DataField]
    public float OverdoseThreshold = 35f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-convermol",
            ("chance", Probability),
            ("rate", HealPerTick),
            ("ratio", ToxRatio),
            ("od", OverdoseThreshold));
}

public sealed partial class ChemConvermolEntityEffectSystem
    : EntityEffectSystem<DamageableComponent, ChemConvermol>
{
    private static readonly ReagentId Convermol = new("Convermol", null);

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    protected override void Effect(Entity<DamageableComponent> entity, ref EntityEffectEvent<ChemConvermol> args)
    {
        if (!TryComp<BloodstreamComponent>(entity, out var bloodstream))
            return;

        if (!_solution.ResolveSolution(
                entity.Owner,
                bloodstream.BloodSolutionName,
                ref bloodstream.BloodSolution,
                out var bloodSolution))
            return;

        var effect = args.Effect;
        var groupProto = _prototype.Index(effect.HealDamageGroup);
        var convermolQuantity = FixedPoint2.Zero;

        if (bloodSolution.TryGetReagentQuantity(Convermol, out var quantity))
            convermolQuantity = quantity;

        float currentDamage = 0f;
        var damageByType = new Dictionary<string, float>();

        foreach (var damageTypeId in groupProto.DamageTypes)
        {
            if (!entity.Comp.Damage.DamageDict.TryGetValue(damageTypeId, out var value))
                continue;

            var damage = value.Float();
            if (damage <= 0f)
                continue;

            damageByType[damageTypeId] = damage;
            currentDamage += damage;
        }

        var potential = effect.HealPerTick * args.Scale;
        var overdosed = convermolQuantity.Float() >= effect.OverdoseThreshold;
        var actualHeal = overdosed
            ? potential
            : Math.Max(0f, Math.Min(potential, currentDamage + effect.Buffer));

        // Buffer must produce Poison even when Airloss is zero.
        if (actualHeal > 0f && currentDamage > 0f)
        {
            var healSpec = new DamageSpecifier();
            foreach (var (typeId, damage) in damageByType)
            {
                healSpec.DamageDict[typeId] = FixedPoint2.New(-(actualHeal * damage / currentDamage));
            }

            _damageable.TryChangeDamage(entity.Owner, healSpec, true, interruptsDoAfters: false);
        }

        var tox = actualHeal / effect.ToxRatio;
        if (tox > 0f)
        {
            var toxSpec = new DamageSpecifier();
            toxSpec.DamageDict[effect.ToxDamageType] = FixedPoint2.New(tox);
            _damageable.TryChangeDamage(entity.Owner, toxSpec, true, interruptsDoAfters: false);
        }
    }
}
