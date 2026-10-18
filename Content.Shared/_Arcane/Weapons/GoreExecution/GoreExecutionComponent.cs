using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Weapons.GoreExecution;

[RegisterComponent]
public sealed partial class GoreExecutionComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionArcaneGoreExecution";

    [DataField]
    public bool Active;

    [DataField]
    public EntityUid? ActionEntity;
}
