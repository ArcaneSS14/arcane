using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Leash;

/// <summary>
/// Leash component
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LeashComponent : Component
{
    /// <summary>
    /// The entity that the leash is currently attached to
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? AttachedEntity;

    /// <summary>
    /// Maximum leash distance
    /// </summary>
    [DataField]
    public float MaxDistance = 3.0f;

    /// <summary>
    /// Breakaway distance with a margin
    /// </summary>
    public float SnapDistance => MaxDistance + 1.5f;
    public float SnapDistanceSq => SnapDistance * SnapDistance;

    /// <summary>
    /// Connection ID
    /// </summary>
    [DataField]
    public string? JointId;
}
