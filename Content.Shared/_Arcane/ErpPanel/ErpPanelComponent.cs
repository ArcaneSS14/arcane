using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.ErpPanel;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ErpPanelOwnerComponent : Component
{
    public EntityUid? Target = null;

    public Dictionary<string, TimeSpan> Cooldowns = new();

    [DataField, AutoNetworkedField]
    public float ArousalMultiplier = 100f;

    [DataField, AutoNetworkedField]
    public float MoanMultiplier = 100f;
}
