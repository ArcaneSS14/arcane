// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Clothing.Components;

/// <summary>
/// Clothing whose visual layers erase pixels from the wearer's composite sprite.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ClothingSpriteCutoutComponent : Component
{
}
