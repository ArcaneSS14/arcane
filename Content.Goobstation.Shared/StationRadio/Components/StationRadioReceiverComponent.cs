using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.StationRadio.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)] // Arcane-Edit
public sealed partial class StationRadioReceiverComponent : Component
{
    // Arcane-Start
    /// <summary>
    /// The resolved media currently being played through this receiver.
    /// </summary>
    [AutoNetworkedField]
    public ResolvedSoundSpecifier? CurrentMedia;

    /// <summary>
    /// Server time when the current media started playing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? MediaStartTime;
    // Arcane-End

    /// <summary>
    /// Changes every time playback starts, including when the same media is replayed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int PlaybackId; // Arcane-Edit

    /// <summary>
    /// Is the radio turned on
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active = true;

    /// <summary>
    /// Default audio params for the played audio.
    /// </summary>
    [DataField, AutoNetworkedField]
    public AudioParams DefaultParams = AudioParams.Default.WithVolume(3.5f).WithMaxDistance(8f); // 8 is just the edge of the screen usually
}
