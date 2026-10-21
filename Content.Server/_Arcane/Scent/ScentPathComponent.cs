using Robust.Shared.Map;

namespace Content.Server._Arcane.Scent;

[RegisterComponent, Access(typeof(ScentPathSystem))]
public sealed partial class ScentPathComponent : Component
{
    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromMinutes(3);

    [DataField]
    public float MinPressure = 20f;

    [DataField]
    public TimeSpan CarryTime = TimeSpan.FromSeconds(35);

    [ViewVariables]
    public readonly Dictionary<(EntityUid Grid, Vector2i Tile), (EntityCoordinates Coordinates, TimeSpan Time)> Points = new();

    // A tile walked twice is queued twice, the entry is stale if its time differs from Points.
    [ViewVariables]
    public readonly Queue<((EntityUid Grid, Vector2i Tile) Key, TimeSpan Time)> Order = new();

    [ViewVariables]
    public string Scent = string.Empty;

    [ViewVariables]
    public EntityUid? LastGrid;

    [ViewVariables]
    public Vector2i LastTile;
}

[RegisterComponent, Access(typeof(ScentPathSystem))]
public sealed partial class ScentPathViewerComponent : Component
{
    [ViewVariables]
    public string Scent = string.Empty;

    [ViewVariables]
    public readonly Dictionary<(EntityUid Grid, Vector2i Tile), TimeSpan> Sent = new();
}

[RegisterComponent, Access(typeof(ScentPathSystem))]
public sealed partial class ScentCarryComponent : Component
{
    [ViewVariables]
    public EntityUid Holder;

    [ViewVariables]
    public TimeSpan Since;
}
