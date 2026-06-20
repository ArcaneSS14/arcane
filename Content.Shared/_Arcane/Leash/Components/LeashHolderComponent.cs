namespace Content.Shared._Arcane.Leash.Components;

[RegisterComponent, AutoGenerateComponentState]
public sealed partial class LeashHolderComponent : Component
{
    [AutoNetworkedField]
    public readonly List<EntityUid> Leashes = new();
}
