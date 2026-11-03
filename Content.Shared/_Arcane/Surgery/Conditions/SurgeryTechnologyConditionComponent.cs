using Content.Shared.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery.Conditions;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryTechnologyConditionComponent : Component
{
    [DataField(required: true)]
    public ProtoId<TechnologyPrototype> Technology;

    [DataField]
    public bool Inverse;

    [DataField]
    public ProtoId<TechnologyPrototype>? ReplacedBy;
}
