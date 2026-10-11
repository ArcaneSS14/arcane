using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Weapons.TargetLock;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TargetLockComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionArcaneTargetLock";

    [DataField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? LockedTarget;

    [DataField, AutoNetworkedField]
    public EntityUid? User;

    [DataField]
    public float Range = 25f;
}
