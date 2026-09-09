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
}