using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.InclothingTools;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AttachedToolsComponent : Component
{

    [DataField, AutoNetworkedField]
    public EntityUid AttachedUid;
}