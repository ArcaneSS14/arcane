using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ConflictWear.Components;

[RegisterComponent]
public sealed partial class ConflictWearComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<ConflictWearRulePrototype>> Rules = new();
}
