using Robust.Shared.Configuration;

namespace Content.Shared._Arcane.CCVars;

public sealed partial class ACCVars
{
    // How many times slower than ideal a step may be before the extra time turns into failure chance.
    public static readonly CVarDef<float> SurgeryFailureThreshold =
        CVarDef.Create("surgery.failure_threshold", 2.5f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> SurgeryFailureChancePerSecond =
        CVarDef.Create("surgery.failure_chance_per_second", 0.1f, CVar.SERVER | CVar.REPLICATED);

    // Counts only toward the failure chance, not the step's duration.
    public static readonly CVarDef<float> SelfSurgeryFailurePenalty =
        CVarDef.Create("surgery.self_surgery_failure_penalty", 0f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> SelfSurgeryFailureChance =
        CVarDef.Create("surgery.self_surgery_failure_chance", 0.2f, CVar.SERVER | CVar.REPLICATED);

    // Anything without its own surgery surface counts as the floor.
    public static readonly CVarDef<float> SurgeryFloorSpeedModifier =
        CVarDef.Create("surgery.floor_speed_modifier", 0.5f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> SurgeryAnesthesiaSpeedModifier =
        CVarDef.Create("surgery.anesthesia_speed_modifier", 1.25f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> SurgeryAnalyzedSpeedModifier =
        CVarDef.Create("surgery.analyzed_speed_modifier", 1.25f, CVar.SERVER | CVar.REPLICATED);
}
