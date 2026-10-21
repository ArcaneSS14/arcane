using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.Scent;

[Serializable, NetSerializable]
public sealed class ScentPathMessage(bool reset, TimeSpan lifetime, List<ScentPathPoint> points) : EntityEventArgs
{
    public readonly bool Reset = reset;
    public readonly TimeSpan Lifetime = lifetime;
    public readonly List<ScentPathPoint> Points = points;
}

[Serializable, NetSerializable]
public readonly record struct ScentPathPoint(NetCoordinates Coordinates, TimeSpan Time);
