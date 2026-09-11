using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.Radio;

[NetSerializable, Serializable]
public enum HeadsetChannelUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class HeadsetChannelUiState : BoundUserInterfaceState
{
    /// <summary>
    ///     The radio channels available on the headset.
    /// </summary>
    public List<ProtoId<RadioChannelPrototype>> Channels;

    public HashSet<int> MutedFrequencies;

    public HeadsetChannelUiState(List<ProtoId<RadioChannelPrototype>> channels, HashSet<int> mutedFrequencies)
    {
        Channels = channels;
        MutedFrequencies = mutedFrequencies;
    }
}

[Serializable, NetSerializable]
public sealed class HeadsetChannelMuteMessage : BoundUserInterfaceMessage
{
    public int Frequency;
    public bool Muted;

    public HeadsetChannelMuteMessage(int frequency, bool muted)
    {
        Frequency = frequency;
        Muted = muted;
    }
}
