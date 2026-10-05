// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace Content.Shared._Arcane.Inventory;

/// <summary>
///     How long the wearer spends putting this item on and taking it off.
///     Read by <see cref="SharedArcaneInventorySystem"/> when an item is equipped to or unequipped from
///     the entity itself. While this component exists it decides the delay on its own, overriding
///     <see cref="Content.Shared.Clothing.Components.ClothingComponent"/> equip/unequip delays; without it the
///     clothing delay applies, then <see cref="DefaultDelay"/>.
/// </summary>
[RegisterComponent]
public sealed partial class InventoryEquipDelayComponent : Component
{
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(0.4);

    [DataField]
    public TimeSpan EquipDelay = DefaultDelay;

    [DataField]
    public TimeSpan UnequipDelay = DefaultDelay;
}
