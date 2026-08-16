using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Anomaly;

[RegisterComponent, Access(typeof(PeriodicAnomalySpawnerSystem))]
public sealed partial class PeriodicAnomalySpawnerComponent : Component
{
    [DataField]
    public TimeSpan Interval = TimeSpan.FromMinutes(5);

    [DataField]
    public float Chance = 0.05f;

    [DataField]
    public EntProtoId AnomalySpawnerPrototype = "RandomAnomalySpawner";

    public TimeSpan NextAttempt;
}
