using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.SiliconStanding;

/// <summary>
/// Tag component present on a silicon entity while it is in the resting state.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SiliconRestingComponent : Component
{
}
