using Content.Shared.DoAfter;
using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.WashingMachine;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WashingMachineStuckComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Machine;

    public EntityUid? TopVisual;

    public DoAfterId? EscapeDoAfter;

    public Dictionary<object, bool> PreLayerVisibility = new();
}

