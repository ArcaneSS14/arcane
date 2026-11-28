using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Silicons;

/// <summary>
/// Marker component that grants a silicon entity all ERP-related components on MapInit.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SiliconInteractableComponent : Component
{
}
