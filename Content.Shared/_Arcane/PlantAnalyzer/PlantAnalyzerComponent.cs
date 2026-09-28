using Robust.Shared.Audio;
using Robust.Shared.GameStates;

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
    public SoundSpecifier? scanSound;

    /// <summary>
    /// Object to be scanned
    /// </summary>
    [ViewVariables]
    public EntityUid? Target;
}
