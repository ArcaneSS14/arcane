using Content.Shared._Arcane.Slime;

namespace Content.Client._Arcane.Slime;

/// <summary>
/// Client part of the slime limb regrow. The shared system runs during prediction and
/// shows the popup/audio feedback immediately so the action feels responsive.
/// </summary>
public sealed partial class SlimeRegrowSystem : SharedSlimeRegrowSystem
{
}
