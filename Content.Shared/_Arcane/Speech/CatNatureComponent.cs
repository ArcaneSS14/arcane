using Robust.Shared.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Arcane.Speech;

[RegisterComponent]
public sealed partial class CatNatureComponent : Component
{
    [DataField("MeowSound")]
    public SoundSpecifier? MeowSound;

    [DataField("MewSound")]
    public SoundSpecifier? MewSound;

    [DataField("GrowlSound")]
    public SoundSpecifier? GrowlSound;

    [DataField("PurrSound")]
    public SoundSpecifier? PurrSound;
}
