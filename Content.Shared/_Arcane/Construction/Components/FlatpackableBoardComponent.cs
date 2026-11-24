using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Construction.Components;

// Board for a structure that is neither a machine nor a computer, but can still be flatpacked.
[RegisterComponent]
public sealed partial class FlatpackableBoardComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Prototype;
}
