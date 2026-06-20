using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Leash.Components;

[RegisterComponent, AutoGenerateComponentState]
public sealed partial class CollarComponent : Component
{
    [DataField]
    public TimeSpan BreakoutTime = TimeSpan.FromSeconds(4);

    [DataField]
    public ProtoId<AlertPrototype> Alert = "Collared";

    [AutoNetworkedField]
     public EntityUid? Wearer;
    [AutoNetworkedField]
     public EntityUid? AttachedLeash;
}
