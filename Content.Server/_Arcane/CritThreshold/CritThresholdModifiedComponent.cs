using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Mobs;

namespace Content.Server._Arcane.CritThreshold;

/// <summary>
///     The thresholds a mob had before they were scaled, kept only while a modifier is active.
/// </summary>
[RegisterComponent]
public sealed partial class CritThresholdModifiedComponent : Component
{
    [DataField]
    public Dictionary<MobState, FixedPoint2> Baseline = new();

    [DataField]
    public float Modifier = 1f;
}
