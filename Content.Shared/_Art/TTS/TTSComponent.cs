using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Art.TTS;

[RegisterComponent, NetworkedComponent]
public sealed partial class TTSComponent : Component
{
    [DataField("voice")]
    public ProtoId<TTSVoicePrototype>? VoicePrototype = "Gman";

    [DataField]
    public string? Effect;
}
