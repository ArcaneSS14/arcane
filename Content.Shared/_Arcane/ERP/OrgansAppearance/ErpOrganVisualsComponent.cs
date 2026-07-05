using Content.Shared._Arcane.ERP.Preferences;
using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.ERP.OrgansAppearance;

/// <summary>
/// Holds organ appearance data for a humanoid entity.
/// Populated server-side from ErpOrganPreferences, synced to clients automatically.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class ErpOrganVisualsComponent : Component
{
    [AutoNetworkedField]
    public Dictionary<string, ErpOrganConfig> Organs { get; set; } = [];

    /// <summary>Slots whose covering clothing is currently worn. Managed by EroticCoverageSystem.</summary>
    [AutoNetworkedField]
    public HashSet<string> CoveredSlots { get; set; } = [];

    /// <summary>Slots hidden when not aroused. Set from EroticOrgansComponent.HideWhenFlaccid.</summary>
    [AutoNetworkedField]
    public HashSet<string> HideWhenFlaccid { get; set; } = [];
}
