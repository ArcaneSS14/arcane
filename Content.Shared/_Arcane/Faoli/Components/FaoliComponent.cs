using Robust.Shared.GameStates;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Faoli.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class FaoliComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public FixedPoint2 Faoli = 50;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 OverflowLimit = 150;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 Limit = 100;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 Maximum = 50;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 Low = 15;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Interval = 2f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float OverflowInterval = 4f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float LowInterval = 1f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 Regeneartion = 1;

    [DataField]
    public TimeSpan NextTickTime;

    [DataField]
    public ProtoId<AlertPrototype> FaoliAlert = "Faoli";
}
