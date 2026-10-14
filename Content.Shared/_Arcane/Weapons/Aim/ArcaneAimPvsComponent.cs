using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Weapons.Aim;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ArcaneAimPvsComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Increase = 1.6f;
}
