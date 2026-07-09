using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.ErpPanel;

[RegisterComponent, NetworkedComponent]
public sealed partial class ErpPanelOwnerComponent : Component
{
    [DataField]
    public bool Enabled = true;

    public EntityUid? Target = null;

    public Dictionary<string, TimeSpan> Cooldowns = new();
}
