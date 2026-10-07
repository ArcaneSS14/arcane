// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Shared.Containers;

namespace Content.Shared._Arcane.Inventory;

public enum InventoryDelayResult
{
    Immediate,
    Queued,
    Failed,
}

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
    ///     <see cref="InventoryEquipDelayComponent"/> on the item owns the delay outright, including
    ///     <see cref="TimeSpan.Zero"/> for an instant equip. Without the component the clothing delay wins,
    ///     then <see cref="InventoryEquipDelayComponent.DefaultDelay"/>.
    /// </summary>
    public TimeSpan GetEquipDelay(EntityUid item, SlotDefinition slotDefinition, ClothingComponent? clothing = null)
    {
        if (CompOrNull<InventoryEquipDelayComponent>(item) is { } equipDelay)
            return equipDelay.EquipDelay;

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
        if (CompOrNull<InventoryEquipDelayComponent>(item) is { } equipDelay)
            return equipDelay.UnequipDelay;

        if (Resolve(item, ref clothing, false)
            && clothing.UnequipDelay > TimeSpan.Zero
            && (clothing.Slots & slotDefinition.SlotFlags) != 0)
        {
            return clothing.UnequipDelay;
        }

        return InventoryEquipDelayComponent.DefaultDelay;
    }

    /// <summary>
    ///     Applies the self-equip delay to <paramref name="item"/>. See <see cref="InventoryDelayResult"/>.
    /// </summary>
    public InventoryDelayResult TryStartEquipDoAfter(
        EntityUid actor,
        EntityUid target,
        EntityUid item,
        string slot,
        BaseContainer slotContainer,
        SlotDefinition slotDefinition,
        ClothingComponent? clothing = null,
        EntityUid? handBack = null)
    {
        if (!_container.CanInsert(item, slotContainer))
            return InventoryDelayResult.Immediate;

        var delay = GetEquipDelay(item, slotDefinition, clothing);
        if (delay <= TimeSpan.Zero)
            return InventoryDelayResult.Immediate;

        var handBackNet = handBack == null ? (NetEntity?) null : GetNetEntity(handBack.Value);

        var args = new DoAfterArgs(
            EntityManager,
            actor,
            delay,
            new InventoryDoAfterEvent(true, slot, handBack: handBackNet),
            item,
            target,
            item)
        {
            BreakOnMove = false,
            NeedHand = true,
        };

        return _doAfter.TryStartDoAfter(args)
            ? InventoryDelayResult.Queued
            : InventoryDelayResult.Failed;
    }

    /// <summary>
    ///     Applies the self-unequip delay to <paramref name="item"/>, queueing
    ///     <paramref name="equipAfter"/> into the freed slot when the wait is over.
    ///     See <see cref="InventoryDelayResult"/>.
    /// </summary>
    public InventoryDelayResult TryStartUnequipDoAfter(
        EntityUid actor,
        EntityUid target,
        EntityUid item,
        string slot,
        SlotDefinition slotDefinition,
        ClothingComponent? clothing = null,
        EntityUid? equipAfter = null)
    {
        var delay = GetUnequipDelay(item, slotDefinition, clothing);
        if (delay <= TimeSpan.Zero)
            return InventoryDelayResult.Immediate;

        var equipAfterNet = equipAfter == null ? (NetEntity?) null : GetNetEntity(equipAfter.Value);

        var args = new DoAfterArgs(
            EntityManager,
            actor,
            delay,
            new InventoryDoAfterEvent(false, slot, equipAfterNet),
            item,
            target,
            item)
        {
            BreakOnMove = false,
            NeedHand = true,
        };

        return _doAfter.TryStartDoAfter(args)
            ? InventoryDelayResult.Queued
            : InventoryDelayResult.Failed;
    }

    private void OnInventoryDoAfter(Entity<ItemComponent> ent, ref InventoryDoAfterEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        var actor = args.User;

        if (args.Cancelled)
        {
            if (args.HandBack is { } handBack && TryGetEntity(handBack, out var handBackEnt) && !TerminatingOrDeleted(handBackEnt.Value))
            {
                _hands.PickupOrDrop(actor, handBackEnt.Value);
            }

            return;
        }

        if (args.Equip)
        {
            args.Handled = _inventory.TryEquip(actor, target, ent.Owner, args.Slot, predicted: true, checkDoafter: false, triggerHandContact: true);

            if (args.Handled && args.HandBack is { } handBack && TryGetEntity(handBack, out var handBackEnt) && !TerminatingOrDeleted(handBackEnt.Value))
            {
                _hands.PickupOrDrop(actor, handBackEnt.Value);
            }

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
            _inventory.TryEquipWithHandBack(actor, target, equipAfterEnt.Value, args.Slot, ent.Owner, out var doAfterStarted);
            if (doAfterStarted)
                return;
        }

        _hands.PickupOrDrop(actor, ent.Owner);
    }
}
