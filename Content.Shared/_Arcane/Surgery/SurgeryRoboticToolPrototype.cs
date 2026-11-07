using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.Surgery;

/// <summary>
///     The mechanical tool a robotic part needs in place of a surgical one; the ID is the surgical tool's component name.
/// </summary>
[Prototype]
public sealed partial class SurgeryRoboticToolPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ComponentRegistry Tool = new();

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;
}
