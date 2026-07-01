using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects;

public sealed partial class Oxygenate : EventEntityEffect<Oxygenate>
{
    [DataField]
    public float Factor = 1f;

    // Arcane-Start
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-oxygenate",
            ("chance", Probability),
            ("factor", Factor));
    // Arcane-End

    /* Arcane-Edit-Start
    // JUSTIFICATION: This is internal magic that players never directly interact with.
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
    */ // Arcane-Edit-End
}
