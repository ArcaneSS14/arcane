using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Invisibility;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedArcaneInvisibilitySystem))]
public sealed partial class ArcaneInvisibilityComponent : Component
{
    [DataField("shaderVisibility")]
    [AutoNetworkedField]
    public float ShaderVisibility = 0.01f;
}
