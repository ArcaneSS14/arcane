namespace Content.Shared._Arcane.CritThreshold;

// Status effects only count the strongest raise and the strongest drop, other sources multiply into Modifier.
[ByRefEvent]
public record struct RefreshCritThresholdEvent()
{
    public float Raise = 1f;
    public float Drop = 1f;
    public float Modifier = 1f;
}
