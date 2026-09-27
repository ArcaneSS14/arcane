// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.Inventory; // Arcane de facto

[Serializable, NetSerializable]
public sealed partial class InventoryDoAfterEvent : SimpleDoAfterEvent
{
    public readonly bool Equip;
    public readonly string Slot;

    public InventoryDoAfterEvent(bool equip, string slot)
    {
        Equip = equip;
        Slot = slot;
    }
}
