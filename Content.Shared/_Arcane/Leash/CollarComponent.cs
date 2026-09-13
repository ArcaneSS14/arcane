using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Leash;

/// <summary>
/// A collar component. Allows you to attach a LeashComponent to an entity that is wearing this item.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CollarComponent : Component
{
    // A place to add sounds to the collars. Empty for now
}
