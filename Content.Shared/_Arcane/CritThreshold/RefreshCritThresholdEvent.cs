namespace Content.Shared._Arcane.CritThreshold;

/// <summary>
///     Raised on a mob to collect what its crit and death thresholds are multiplied by.
///     Status effects only count the strongest raise and the strongest drop, other sources multiply into <see cref="Modifier"/>.
/// </summary>
[ByRefEvent]
public record struct RefreshCritThresholdEvent()
{
    public float Raise = 1f;
    public float Drop = 1f;
    public float Modifier = 1f;
}
