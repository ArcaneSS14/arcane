using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Leash.Components;

[RegisterComponent]
public sealed partial class CollarComponent : Component
{
    [DataField]
    public TimeSpan BreakoutTime = TimeSpan.FromSeconds(4);

    [DataField]
    public ProtoId<AlertPrototype> Alert = "Collared";

    public EntityUid? Wearer;
    public EntityUid? AttachedLeash;
}
