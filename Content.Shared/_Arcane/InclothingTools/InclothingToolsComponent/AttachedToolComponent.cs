using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.InclothingTools;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AttachedToolComponent : Component
{

    [DataField, AutoNetworkedField]
    public EntityUid AttachedUid;

    [DataField, AutoNetworkedField]
    public EntityUid? PairedTool;
}