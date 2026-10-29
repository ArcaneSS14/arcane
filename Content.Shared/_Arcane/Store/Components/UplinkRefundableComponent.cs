using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Store.Components;

[RegisterComponent]
public sealed partial class UplinkRefundableComponent : Component
{
    [DataField]
    public EntityUid OriginalUplink;

    [DataField]
    public FixedPoint2 RefundAmount;

    [DataField]
    public bool IsUsed;

    [DataField]
    public ProtoId<CurrencyPrototype> Currency = "Telecrystal";

    [DataField]
    public List<string>? OriginalContainerPrototypes;

    [DataField]
    public Dictionary<string, FixedPoint2>? OriginalSolutionReagents;
}
