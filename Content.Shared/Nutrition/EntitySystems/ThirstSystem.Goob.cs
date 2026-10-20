using Content.Shared.Nutrition.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared.Nutrition.EntitySystems;

public sealed partial class ThirstSystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    private void OnShutdown(EntityUid uid, ThirstComponent component, ComponentShutdown args)
    {
        _alerts.ClearAlertCategory(uid, component.ThirstyCategory);
        _movement.RefreshMovementSpeedModifiers(uid);
    }
}
