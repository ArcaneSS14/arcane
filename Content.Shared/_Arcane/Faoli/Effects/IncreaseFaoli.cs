using Content.Shared._Arcane.Faoli.Components;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Content.Goobstation.Maths.FixedPoint;

namespace Content.Shared._Arcane.Faoli.Effects;

[UsedImplicitly]
public sealed partial class IncreaseFaoliSystem : EntityEffectSystem<FaoliComponent, IncreaseFaoli>
{
    [Dependency] private readonly SharedFaoliSystem _faoli = default!;

    protected override void Effect(Entity<FaoliComponent> ent, ref EntityEffectEvent<IncreaseFaoli> args)
    {
        var amount = args.Effect.Amount * args.Scale;

        if (amount > 0)
        {
            var current = ent.Comp.Faoli;
            var maxIncrease = args.Effect.Maximum - current;

            if (maxIncrease <= 0)
                return;

            amount = FixedPoint2.Min(amount, maxIncrease);
        }

        if (amount == 0f)
            return;

        _faoli.TryChangeFaoliAmount(ent.Owner, amount, ent.Comp);
    }
}

public sealed partial class IncreaseFaoli : EntityEffectBase<IncreaseFaoli>
{
    [DataField]
    public FixedPoint2 Amount = 1f;

    [DataField]
    public FixedPoint2 Maximum = 150f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-increase-faoli",
            ("amount", Amount),
            ("maximum", Maximum));
    }
}
