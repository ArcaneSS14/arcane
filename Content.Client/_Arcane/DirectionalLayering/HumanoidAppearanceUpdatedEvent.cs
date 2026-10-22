using Robust.Shared.GameObjects;

namespace Content.Client.Humanoid;

/// <summary>
///     Raised (broadcast) after a humanoid's sprite layers have been rebuilt by
///     <see cref="HumanoidAppearanceSystem.UpdateSprite(Entity{Content.Shared.Humanoid.HumanoidAppearanceComponent, Robust.Client.GameObjects.SpriteComponent})"/>.
///     Systems that reorder sprite layers against the camera (e.g. directional layering) react to this instead of
///     guessing when the appearance changed.
/// </summary>
public sealed class HumanoidAppearanceUpdatedEvent : EntityEventArgs
{
}
