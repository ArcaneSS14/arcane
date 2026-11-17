using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Surgery.Tools;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryMultitoolComponent : Component, ISurgeryToolComponent
{
    public string ToolName => Loc.GetString("surgery-tool-multitool");

    [DataField]
    public bool? Used { get; set; } = null;

    [DataField]
    public float Speed { get; set; } = 1f;
}
