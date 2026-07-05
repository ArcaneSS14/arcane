using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Speech;

[RegisterComponent]
public sealed partial class CatNatureComponent : Component
{
    public Dictionary<Sex, ProtoId<EmoteSoundsPrototype>>? OriginalSounds;
    public ProtoId<EmoteSoundsPrototype>? OriginalEmoteSounds;
}
