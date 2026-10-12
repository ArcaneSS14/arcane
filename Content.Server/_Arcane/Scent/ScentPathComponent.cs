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
    public TimeSpan CarryTime = TimeSpan.FromMinutes(5);

    [ViewVariables]
    public readonly List<(EntityCoordinates Coordinates, TimeSpan Time)> Points = new();

    [ViewVariables]
    public string Scent = string.Empty;

    [ViewVariables]
    public EntityUid? LastGrid;

    [ViewVariables]
    public Vector2i LastTile;
}

[RegisterComponent, Access(typeof(ScentPathSystem))]
public sealed partial class ScentCarryComponent : Component
{
    [ViewVariables]
    public EntityUid Holder;

    [ViewVariables]
    public TimeSpan Since;
}
