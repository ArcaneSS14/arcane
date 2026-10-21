using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orion.Antag.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class GlobalAntagonistComponent : Component
{
    [DataField(required: true)]
    public string? AntagonistPrototype;
}
