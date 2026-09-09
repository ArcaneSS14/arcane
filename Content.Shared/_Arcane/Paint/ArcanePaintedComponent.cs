using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.Paint;

/// Component applied to target entity when painted
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ArcanePaintedComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#2cdbd5");

    [DataField, AutoNetworkedField]
    public bool Enabled;

    // Not using ProtoId because ShaderPrototype is in Robust.Client
    [DataField, AutoNetworkedField]
    public string ShaderName = "Greyscale";

    /// Client-only per-layer original colors keyed by layer index.
    /// Populated by PaintVisualizerSystem before paint is applied
    /// and restored on component shutdown.
    [DataField]
    public Dictionary<int, Color> LayerColors = new();
}

[Serializable, NetSerializable]
public enum PaintVisuals : byte
{
    Painted,
}
