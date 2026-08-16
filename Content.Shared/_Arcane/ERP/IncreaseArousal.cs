using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ERP;

[UsedImplicitly]
public sealed partial class IncreaseArousalSystem : EntityEffectSystem<ArousalComponent, IncreaseArousal>
{
    [Dependency] private readonly ArousalSystem _arousal = default!;

    protected override void Effect(Entity<ArousalComponent> entity, ref EntityEffectEvent<IncreaseArousal> args)
    {
        var current = _arousal.GetArousal(entity.Comp);
        if (current >= args.Effect.Maximum)
            return;

        var amount = MathF.Min(args.Effect.Amount * args.Scale, args.Effect.Maximum - current);
        if (amount <= 0f)
            return;

        _arousal.AddArousal(entity.Owner, amount, entity.Comp);
    }
}

public sealed partial class IncreaseArousal : EntityEffectBase<IncreaseArousal>
{
    [DataField]
    public float Amount = 1f;

    [DataField]
    public float Maximum = 60f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-increase-arousal",
            ("amount", Amount),
            ("maximum", Maximum));
    }
}
