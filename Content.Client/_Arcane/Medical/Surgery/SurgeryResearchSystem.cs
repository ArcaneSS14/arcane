using Content.Shared._Arcane.Surgery;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client._Arcane.Medical.Surgery;

// The research server is outside the client's view, so the server keeps the surgeon's researched technologies on them.
public sealed class SurgeryResearchSystem : SharedSurgeryResearchSystem
{
    protected override IReadOnlyCollection<ProtoId<TechnologyPrototype>> GetResearchedTechnologies(EntityUid user)
    {
        return CompOrNull<SurgeryResearchAccessComponent>(user)?.Technologies ?? [];
    }
}
