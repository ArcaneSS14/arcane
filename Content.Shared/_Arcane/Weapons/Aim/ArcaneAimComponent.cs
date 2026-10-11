using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Weapons.Aim;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class ArcaneAimComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionArcaneAim";

    [DataField]
    public EntityUid? ActionEntity;

    [DataField]
    public float Offset = 8f;

    [DataField]
    public float PvsIncrease = 0.8f;

    [DataField]
    public float OffsetSpeed = 0.5f;

    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public EntityUid? User;
}
