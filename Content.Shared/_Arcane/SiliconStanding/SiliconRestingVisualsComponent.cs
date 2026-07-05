namespace Content.Shared._Arcane.SiliconStanding;

/// <summary>
/// Specifies explicit body RSI states for a silicon entity that has resting sprites.
/// Only entities with this component will have their body sprite changed when resting.
/// </summary>
[RegisterComponent]
public sealed partial class SiliconRestingVisualsComponent : Component
{
    [DataField]
    public string NormalBodyState = "robot";

    [DataField]
    public string RestBodyState = "robot_rest";
}
