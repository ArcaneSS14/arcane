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
    public EntityUid? ActionUid;

    [DataField, AutoNetworkedField]
    public string ContainerId = "inclothing-tools";

    [ViewVariables]
    public Container Container = default!;

    [DataField, AutoNetworkedField]
    public List<EntProtoId>? ToolPrototypes;

    [DataField, AutoNetworkedField]
    public List<EntityUid> ToolsUids = new();

    [DataField, AutoNetworkedField]
    public int MaxEquippedTools = 2;

    [DataField, AutoNetworkedField]
    public int EquippedTools = 0;

    [DataField, AutoNetworkedField]
    public bool ForseDropHeldItem = false;

}