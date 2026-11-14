using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

/// <summary>
///     A kind of body told apart by species and components; in a list, the first matching kind wins.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class SurgeryBodyKind
{
    [DataField]
    public List<ProtoId<SpeciesPrototype>> Species = new();

    [DataField]
    public EntityWhitelist? Whitelist;
}
