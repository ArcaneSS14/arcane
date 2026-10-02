using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.GameObjects;

namespace Content.Shared._Arcane.PlantAnalyzer;

/// <summary>
/// Plant analyzer component
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlantAnalyzerComponent : Component
{
    /// <summary>
    /// Sound effect played during scanning
    /// </summary>
    [DataField]
    public SoundSpecifier? ScanSound;

    /// <summary>
    /// Object to be scanned
    /// </summary>
    [ViewVariables]
    public EntityUid? Target;

    /// <summary>
    /// ID of the person using the analyzer
    /// </summary>
    [ViewVariables]
    public EntityUid? UiUser;
}
