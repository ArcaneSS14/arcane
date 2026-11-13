using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

/// <summary>
/// Which surgeons use this research server for research-gated surgeries, autopsies and dissections.
/// </summary>
[RegisterComponent]
public sealed partial class SurgeryResearchServerComponent : Component
{
    [DataField(required: true)]
    public ProtoId<NpcFactionPrototype> Faction;

    // The surgeon must also hold a job on the station that owns the server.
    [DataField]
    public bool StationCrew;
}
