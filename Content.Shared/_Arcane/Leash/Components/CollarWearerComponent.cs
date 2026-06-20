namespace Content.Shared._Arcane.Leash.Components;

[RegisterComponent, AutoGenerateComponentState]
public sealed partial class CollarWearerComponent : Component
{
    [AutoNetworkedField]
    public EntityUid? Collar;
}
