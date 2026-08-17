using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Art.TTS;

/// <summary>
/// Raised when a silicon uses the voice-change action.
/// </summary>
public sealed partial class TTSVoiceChangeOpenMenuEvent : InstantActionEvent
{
}

/// <summary>
/// Sent by the server to open the voice selection menu on the client.
/// </summary>
[Serializable, NetSerializable]
public sealed class TTSVoiceChangeMenuMessage(List<ProtoId<TTSVoicePrototype>> voices) : EntityEventArgs
{
    public List<ProtoId<TTSVoicePrototype>> Voices { get; } = voices;
}

/// <summary>
/// Sent by the client with the voice the player selected.
/// </summary>
[Serializable, NetSerializable]
public sealed class TTSVoiceChangeSelectedMessage(ProtoId<TTSVoicePrototype> voice) : EntityEventArgs
{
    public ProtoId<TTSVoicePrototype> Voice { get; } = voice;
}
