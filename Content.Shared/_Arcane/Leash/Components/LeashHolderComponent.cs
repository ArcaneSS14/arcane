namespace Content.Shared._Arcane.Leash.Components;

[RegisterComponent]
public sealed partial class LeashHolderComponent : Component
{
    public readonly HashSet<EntityUid> Leashes = new();
}
