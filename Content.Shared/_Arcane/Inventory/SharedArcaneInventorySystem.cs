// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Shared.Containers;

namespace Content.Shared._Arcane.Inventory;

/// <summary>
///     Owns the Arcane equip/unequip delay and the doafter that resolves it, so that
///     <see cref="InventorySystem"/> stays free of hardcoded delays.
/// </summary>
public sealed class SharedArcaneInventorySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemComponent, InventoryDoAfterEvent>(OnInventoryDoAfter);
    }

    /// <summary>
    ///     <see cref="InventoryEquipDelayComponent"/> wins over clothing, clothing wins over
    ///     <see cref="InventoryEquipDelayComponent.DefaultDelay"/>.
    /// </summary>
    public TimeSpan GetEquipDelay(EntityUid item, SlotDefinition slotDefinition, ClothingComponent? clothing = null)
    {
        if (TryComp<InventoryEquipDelayComponent>(item, out var equipDelay)
            && equipDelay.EquipDelay > TimeSpan.Zero)
        {
            return equipDelay.EquipDelay;
        }

        if (Resolve(item, ref clothing, false)
            && clothing.EquipDelay > TimeSpan.Zero
            && (clothing.Slots & slotDefinition.SlotFlags) != 0)
        {
            return clothing.EquipDelay;
        }

        return InventoryEquipDelayComponent.DefaultDelay;
    }

    /// <inheritdoc cref="GetEquipDelay"/>
    public TimeSpan GetUnequipDelay(EntityUid item, SlotDefinition slotDefinition, ClothingComponent? clothing = null)
    {
        if (TryComp<InventoryEquipDelayComponent>(item, out var equipDelay)
            && equipDelay.UnequipDelay > TimeSpan.Zero)
        {
            return equipDelay.UnequipDelay;
        }

        if (Resolve(item, ref clothing, false)
            && clothing.UnequipDelay > TimeSpan.Zero
            && (clothing.Slots & slotDefinition.SlotFlags) != 0)
        {
            return clothing.UnequipDelay;
        }

        return InventoryEquipDelayComponent.DefaultDelay;
    }

    /// <summary>
    ///     Starts the equip doafter for <paramref name="item"/>. Returns whether it started.
    /// </summary>
    public bool TryStartEquipDoAfter(
        EntityUid actor,
        EntityUid target,
        EntityUid item,
        string slot,
        BaseContainer slotContainer,
        SlotDefinition slotDefinition,
        ClothingComponent? clothing = null)
    {
        if (!_container.CanInsert(item, slotContainer))
            return false;

        var args = new DoAfterArgs(
            EntityManager,
            actor,
            GetEquipDelay(item, slotDefinition, clothing),
            new InventoryDoAfterEvent(true, slot),
            item,
            target,
            item)
        {
            BreakOnMove = false,
            NeedHand = true,
        };

        return _doAfter.TryStartDoAfter(args);
    }

    /// <summary>
    ///     Starts the unequip doafter for <paramref name="item"/>, optionally equipping
    ///     <paramref name="equipAfter"/> into the freed slot once it finishes.
    ///     Returns whether it started.
    /// </summary>
    public bool TryStartUnequipDoAfter(
        EntityUid actor,
        EntityUid target,
        EntityUid item,
        string slot,
        SlotDefinition slotDefinition,
        ClothingComponent? clothing = null,
        EntityUid? equipAfter = null)
    {
        var equipAfterNet = equipAfter == null ? (NetEntity?) null : GetNetEntity(equipAfter.Value);

        var args = new DoAfterArgs(
            EntityManager,
            actor,
            GetUnequipDelay(item, slotDefinition, clothing),
            new InventoryDoAfterEvent(false, slot, equipAfterNet),
            item,
            target,
            item)
        {
            BreakOnMove = false,
            NeedHand = true,
        };

        return _doAfter.TryStartDoAfter(args);
    }

    private void OnInventoryDoAfter(Entity<ItemComponent> ent, ref InventoryDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        var actor = args.User;

        if (args.Equip)
        {
            args.Handled = _inventory.TryEquip(actor, target, ent.Owner, args.Slot, predicted: true, checkDoafter: false, triggerHandContact: true);
            return;
        }

        if (!_inventory.TryGetSlotEntity(target, args.Slot, out var slotEnt) || slotEnt != ent.Owner)
        {
            args.Handled = true;
            return;
        }

        args.Handled = _inventory.TryUnequip(actor, target, args.Slot, predicted: true, checkDoafter: false, triggerHandContact: true);
        if (!args.Handled)
            return;

        if (args.EquipAfter is { } equipAfter
            && TryGetEntity(equipAfter, out var equipAfterEnt)
            && !TerminatingOrDeleted(equipAfterEnt.Value))
        {
            _inventory.TryEquip(actor, target, equipAfterEnt.Value, args.Slot, predicted: true, checkDoafter: true, triggerHandContact: true);
        }

        _hands.PickupOrDrop(actor, ent.Owner);
    }
}
