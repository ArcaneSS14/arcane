using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Store;

namespace Content.Shared._Arcane.Store.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UplinkRefundableComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid OriginalUplink;

    [DataField, AutoNetworkedField]
    public FixedPoint2 RefundAmount;

    [DataField, AutoNetworkedField]
    public bool IsUsed;

    [DataField, AutoNetworkedField]
    public ProtoId<CurrencyPrototype> Currency = "Telecrystal";
}
