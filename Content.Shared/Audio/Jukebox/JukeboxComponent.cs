// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Audio.Jukebox;

[NetworkedComponent, RegisterComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedJukeboxSystem))]
public sealed partial class JukeboxComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<JukeboxPrototype>? SelectedSongId;

    [DataField, AutoNetworkedField]
    // Arcane-Edit-Start
    // public EntityUid? AudioStream;
    public bool Active;

    /// <summary>
    /// Whether the active track is actually producing sound right now (false while paused).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Playing;
    // Arcane-Edit-End

    /// <summary>
    /// RSI state for the jukebox being on.
    /// </summary>
    [DataField]
    public string? OnState;

    /// <summary>
    /// RSI state for the jukebox being on.
    /// </summary>
    [DataField]
    public string? OffState;

    /// <summary>
    /// RSI state for the jukebox track being selected.
    /// </summary>
    [DataField]
    public string? SelectState;

    [ViewVariables]
    public bool Selecting;

    [ViewVariables]
    public float SelectAccumulator;
    // Orion-Start
    [DataField, AutoNetworkedField] // Arcane-Edit
    public float Volume = 50f;

    [DataField] // Arcane
    public float MinVolume = -30f;

    [DataField] // Arcane
    public float MaxVolume = 0f;

    [DataField] // Arcane
    public float MinSlider = 0f;

    [DataField] // Arcane
    public float MaxSlider = 100f;

    [DataField, AutoNetworkedField]
    public bool LoopEnabled;

    [DataField, AutoNetworkedField] // Arcane-Edit
    public TimeSpan? PlaybackStartTime;

    [DataField, AutoNetworkedField] // Arcane-Edit
    public float CurrentPlaybackOffset;

    // Arcane-Start
    [ViewVariables]
    public float TrackLength;
    // Arcane-End
    // Orion-End
}

[Serializable, NetSerializable]
public sealed class JukeboxPlayingMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class JukeboxPauseMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class JukeboxStopMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class JukeboxSelectedMessage(ProtoId<JukeboxPrototype> songId) : BoundUserInterfaceMessage
{
    public ProtoId<JukeboxPrototype> SongId { get; } = songId;
}

[Serializable, NetSerializable]
public sealed class JukeboxSetTimeMessage(float songTime) : BoundUserInterfaceMessage
{
    public float SongTime { get; } = songTime;
}

// Orion-Start
[Serializable, NetSerializable]
public sealed class JukeboxSetVolumeMessage(float volume) : BoundUserInterfaceMessage
{
    public float Volume { get; } = volume;
}

[Serializable, NetSerializable]
public sealed class JukeboxToggleLoopMessage : BoundUserInterfaceMessage;
// Orion-End

[Serializable, NetSerializable]
public enum JukeboxVisuals : byte
{
    VisualState
}

[Serializable, NetSerializable]
public enum JukeboxVisualState : byte
{
    On,
    Off,
    Select,
}

public enum JukeboxVisualLayers : byte
{
    Base
}
