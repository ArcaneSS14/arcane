using Content.Shared.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

/// <summary>
/// Technologies researched by the surgeon's own research network, kept by the server for the surgeon's client.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryResearchAccessComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<TechnologyPrototype>> Technologies = new();

    public override bool SendOnlyToOwner => true;
}
