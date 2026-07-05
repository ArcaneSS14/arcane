using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.SiliconStanding;

/// <summary>
/// Allows a silicon entity to transition between standing and resting states.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class SiliconStandingComponent : Component
{
    public static readonly EntProtoId ToggleRestingActionId = "ActionToggleSiliconResting";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleRestingAction;
}
