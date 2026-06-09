using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Wagging;

/// <summary>
/// Configurable tail markings that grant the wagging action.
/// </summary>
[Prototype("waggingTailGrant")]
public sealed partial class WaggingTailGrantPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ProtoId<MarkingPrototype>> TailMarkings = new();
}
