using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.SpecialWhitelist;

[DataDefinition]
public sealed partial class MarkingWhitelistData
{
    [DataField("allowed")]
    public List<string> Allowed { get; private set; } = new();
}
