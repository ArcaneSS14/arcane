using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Weapons.Aim;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class ArcaneEyeCursorOffsetComponent : Component
{
    [DataField, AutoNetworkedField]
    public float MaxOffset = 8f;

    [DataField, AutoNetworkedField]
    public float OffsetSpeed = 0.5f;

    [DataField, AutoNetworkedField]
    public float PvsIncrease = 1.6f;
}
