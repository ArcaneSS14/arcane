using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.InclothingTools;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RandomInclothingToolsComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Action = "ActionRandomInclothingTool";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}