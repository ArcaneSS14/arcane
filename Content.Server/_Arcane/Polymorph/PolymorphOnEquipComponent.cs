using Robust.Shared.Prototypes;
using Content.Shared.Polymorph;

namespace Content.Server._Arcane.Polymorph;

[RegisterComponent]
public sealed partial class PolymorphOnEquipComponent : Component
{
    [DataField("requiredProto", required: true)]
    public EntProtoId RequiredProto = default!;

    [DataField("polymorphProto", required: true)]
    public ProtoId<PolymorphPrototype> PolymorphProto = default!;
}
