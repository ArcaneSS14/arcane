using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane;

[RegisterComponent]
public sealed partial class AddTagsComponent : Component
{
    [DataField]
    public List<ProtoId<TagPrototype>> Tags = new();
}
