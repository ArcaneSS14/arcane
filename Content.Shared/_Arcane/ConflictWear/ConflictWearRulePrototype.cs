using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ConflictWear;

[Prototype]
public sealed partial class ConflictWearRulePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<string> Slots = new();

    [DataField]
    public List<EntProtoId> ConflictingPrototypes = new();

    [DataField]
    public List<string> ConflictingKnowledge = new();

    [DataField]
    public bool Bidirectional;

    [DataField]
    public LocId? FailPopup = "conflict-wear-fail";
}
