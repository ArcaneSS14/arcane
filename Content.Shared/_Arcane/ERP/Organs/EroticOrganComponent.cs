using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.ERP.Organs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EroticOrganComponent : Component
{
    /// <summary>
    /// Base sensitivity multiplier for arousal calculations. 1.0 = normal.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Sensitivity = 1.0f;

    /// <summary>
    /// Size modifier. 1.0 = average. Used in interaction condition checks and descriptions.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Size = 1.0f;

    /// <summary>
    /// Whether this organ should be configurable in the character editor ERP tab.
    /// </summary>
    [DataField]
    public bool EditorVisible;

    /// <summary>
    /// Visual variants available for this organ in the character editor.
    /// </summary>
    [DataField]
    public List<string> EditorVariants = [];

    /// <summary>
    /// Default visual variant used when the player has no saved preference.
    /// </summary>
    [DataField]
    public string EditorDefaultVariant = "human";

    /// <summary>
    /// Maximum character editor size index. Values below 2 hide the size control.
    /// </summary>
    [DataField]
    public int EditorMaxSize = 1;

    /// <summary>
    /// Whether this organ can use a custom tint instead of skin color.
    /// </summary>
    [DataField]
    public bool EditorAllowColor = true;

    /// <summary>
    /// Whether this organ is currently exposed (not covered by clothing).
    /// Managed server-side by the clothing coverage system for interaction checks.
    /// </summary>
    public bool Visible = true;
}
