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
        var current = ent.Comp.Faoli;
        if (current >= args.Effect.Maximum)
            return;


        var amount = FixedPoint2.Min(args.Effect.Amount * args.Scale, args.Effect.Maximum - current);
        if (amount == 0f)
            return;

        _faoli.ChangeFaoliAmount(ent.Owner, amount, ent.Comp);
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
