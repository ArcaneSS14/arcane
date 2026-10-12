using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.Scent;

[Serializable, NetSerializable]
public sealed class ScentPathMessage(List<ScentPathPoint> points) : EntityEventArgs
{
    public readonly List<ScentPathPoint> Points = points;
}

[Serializable, NetSerializable]
public readonly record struct ScentPathPoint(NetCoordinates Coordinates, TimeSpan Time, TimeSpan Expire);
