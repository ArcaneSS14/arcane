using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.InvisibilityHeart.Components;

[RegisterComponent]
public sealed partial class InvisibilityHeartComponent : Component
{
    [DataField]
    public EntProtoId OrganProto = "OrganInvisibilityHeart";

    [DataField]
    public string OrganSlot = "heart";
}
