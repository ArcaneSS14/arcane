namespace Content.Shared._Arcane.ERP.OrgansAppearance;

/// <summary>
/// Raised on a humanoid entity after its appearance state has been applied on the client.
/// Used to update ERP organ layer visibility without conflicting with the upstream subscription.
/// </summary>
[ByRefEvent]
public record struct HumanoidVisualStateUpdatedEvent;
