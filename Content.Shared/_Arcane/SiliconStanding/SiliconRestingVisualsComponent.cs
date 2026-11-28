using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.SiliconStanding;

/// <summary>
/// Specifies explicit body RSI states for a silicon entity that has resting sprites.
/// Only entities with this component will have their body sprite changed when resting.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SiliconRestingVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public string NormalBodyState = "robot";

    [DataField, AutoNetworkedField]
    public string RestBodyState = "robot_rest";
}
