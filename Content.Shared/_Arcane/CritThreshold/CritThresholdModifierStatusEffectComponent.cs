namespace Content.Shared._Arcane.CritThreshold;

/// <summary>
///     Scales the crit and death thresholds of the mob this status effect is on.
/// </summary>
[RegisterComponent]
public sealed partial class CritThresholdModifierStatusEffectComponent : Component
{
    [DataField]
    public float Modifier = 1f;
}
