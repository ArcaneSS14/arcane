// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.ActionBlocker;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Goobstation.Shared.Interaction;

public sealed class BackEquipSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;

    public override void Initialize()
    {
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.SmartEquipBack,
                InputCmdHandler.FromDelegate(HandleEquipToBack,
                    handle: false,
                    outsidePrediction: false)) // Goobstation - Smart equip to back
            .Register<BackEquipSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        CommandBinds.Unregister<BackEquipSystem>();
    }

    private void HandleEquipToBack(ICommonSession? session)
    {
        HandleEquipToSlot(session, "suitstorage");
    }

    private void HandleEquipToSlot(ICommonSession? session, string equipmentSlot)
    {
        if (session is not { } playerSession)
            return;

        if (playerSession.AttachedEntity is not { Valid: true } uid || !Exists(uid))
            return;

    // Arcane-Start
        TryEquipToSlot(uid, equipmentSlot);
    }

    public bool TryEquipToSlot(EntityUid uid, string equipmentSlot)
    {
    // Arcane-End
        var activeHand = _hands.GetActiveHand(uid);
        if (!TryComp<HandsComponent>(uid, out var hands)
            || activeHand == null)
            return false; // Arcane-Edit

        var handItem = _hands.GetHeldItem((uid, hands), activeHand);

        if (!_actionBlocker.CanInteract(uid, handItem))
            return false; // Arcane-Edit

        if (!TryComp<InventoryComponent>(uid, out var inventory) || !_inventory.HasSlot(uid, equipmentSlot, inventory))
        {
            _popup.PopupClient(Loc.GetString("smart-equip-missing-equipment-slot", ("slotName", equipmentSlot)),
                uid,
                uid);
            return false; // Arcane-Edit
        }

        if (handItem != null && !_hands.CanDropHeld(uid, activeHand))
        {
            _popup.PopupClient(Loc.GetString("smart-equip-cant-drop"), uid, uid);
            return false; // Arcane-Edit
        }

        _inventory.TryGetSlotEntity(uid, equipmentSlot, out var slotEntity);
        var emptyEquipmentSlotString = Loc.GetString("smart-equip-empty-equipment-slot", ("slotName", equipmentSlot));
        if (slotEntity is not { } slotItem)
        {
            if (handItem == null)
            {
                _popup.PopupClient(emptyEquipmentSlotString, uid, uid);
                return false; // Arcane-Edit
            }

            if (!_inventory.CanEquip(uid, handItem.Value, equipmentSlot, out var reason))
            {
                _popup.PopupClient(Loc.GetString(reason), uid, uid);
                return false; // Arcane-Edit
            }

            // _hands.TryDrop((uid, hands), activeHand); // Arcane-Edit
            _inventory.TryEquip(uid, handItem.Value, equipmentSlot, predicted: true, checkDoafter: true);
            return true; // Arcane-Edit
        }
        if (handItem != null)
            return false; // Arcane-Edit

        if (!_inventory.CanUnequip(uid, equipmentSlot, out var inventoryReason))
        {
            _popup.PopupClient(Loc.GetString(inventoryReason), uid, uid);
            return false; // Arcane-Edit
        }
        _inventory.TryUnequip(uid, equipmentSlot, inventory: inventory, predicted: true, checkDoafter: true);
        _hands.TryPickup(uid, slotItem, handsComp: hands);
        return true; // Arcane
    }
}
