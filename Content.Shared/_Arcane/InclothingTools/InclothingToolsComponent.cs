using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.InclothingTools;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class InclothingToolsComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Action = "ActionSelectInclothingTool";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public bool DisableAction = false;

    [DataField, AutoNetworkedField]
    public string ContainerId = "inclothing-tools";

    [ViewVariables]
    public Container Container;
}