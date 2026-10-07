// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.Inventory;

[Serializable, NetSerializable]
public sealed partial class InventoryDoAfterEvent : SimpleDoAfterEvent
{
    public readonly bool Equip;
    public readonly string Slot;
    public readonly NetEntity? EquipAfter;
    public readonly NetEntity? HandBack;

    public InventoryDoAfterEvent(bool equip, string slot, NetEntity? equipAfter = null, NetEntity? handBack = null)
    {
        Equip = equip;
        Slot = slot;
        EquipAfter = equipAfter;
        HandBack = handBack;
    }
}
