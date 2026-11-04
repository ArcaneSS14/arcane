using Robust.Shared.Configuration;

namespace Content.Shared._Arcane.CCVars;

public sealed partial class ACCVars
{
    /// <summary>
    ///     How many times longer than a proper tool on an operating table a surgery step may take before the extra time turns into failure chance.
    /// </summary>
    public static readonly CVarDef<float> SurgeryFailureThreshold =
        CVarDef.Create("surgery.failure_threshold", 2.5f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     Failure chance added by every second a surgery step would take beyond the threshold.
    /// </summary>
    public static readonly CVarDef<float> SurgeryFailureChancePerSecond =
        CVarDef.Create("surgery.failure_chance_per_second", 0.1f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     Added to the step's slowdown when a surgeon operates on themselves, only for the failure chance.
    /// </summary>
    public static readonly CVarDef<float> SelfSurgeryFailurePenalty =
        CVarDef.Create("surgery.self_surgery_failure_penalty", 0f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> SelfSurgeryFailureChance =
        CVarDef.Create("surgery.self_surgery_failure_chance", 0.2f, CVar.SERVER | CVar.REPLICATED);
}
