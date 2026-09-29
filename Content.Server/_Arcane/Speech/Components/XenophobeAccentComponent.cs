using Content.Server.Speech.Prototypes;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Speech.Components;

[RegisterComponent]
public sealed partial class XenophobeAccentComponent : Component
{
    [DataField]
    public ProtoId<ReplacementAccentPrototype> Accent = "xenophobe";

    // applied before the base accent
    [DataField]
    public Dictionary<ProtoId<SpeciesPrototype>, ProtoId<ReplacementAccentPrototype>> SpeciesAccents = new();
}
