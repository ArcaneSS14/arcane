using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Foldable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Robust.Shared.Containers;

namespace Content.Shared._Arcane.Clothing;

public sealed class FoldedHandsClothingSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FoldedHandsClothingComponent, FoldAttemptEvent>(OnFoldAttempt);
        SubscribeLocalEvent<FoldedHandsClothingComponent, FoldedEvent>(OnFolded);
        SubscribeLocalEvent<FoldedHandsClothingComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<FoldedHandsClothingComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    private void OnFoldAttempt(Entity<FoldedHandsClothingComponent> ent, ref FoldAttemptEvent args)
    {
        if (args.Cancelled || args.Comp.IsFolded || !TryGetWearer(ent, out var wearer))
            return;

        if (!CanBlockHands(wearer))
            args.Cancelled = true;
    }

    private void OnFolded(Entity<FoldedHandsClothingComponent> ent, ref FoldedEvent args)
    {
        if (!TryGetWearer(ent, out var wearer))
            return;

        if (args.IsFolded)
            BlockHands(ent, wearer);
        else
            _virtualItem.DeleteInHandsMatching(wearer, ent.Owner);
    }

    private void OnEquipped(Entity<FoldedHandsClothingComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (!TryComp<FoldableComponent>(ent, out var foldable) || !foldable.IsFolded)
            return;

        BlockHands(ent, args.Wearer);
    }

    private void OnUnequipped(Entity<FoldedHandsClothingComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        _virtualItem.DeleteInHandsMatching(args.Wearer, ent.Owner);
    }

    private void BlockHands(Entity<FoldedHandsClothingComponent> ent, EntityUid wearer)
    {
        foreach (var hand in _hands.EnumerateHands(wearer))
        {
            if (_hands.TryGetHeldItem(wearer, hand, out _) && !_hands.TryDrop(wearer, hand))
                continue;

            if (_virtualItem.TrySpawnVirtualItemInHand(ent.Owner, wearer, out var vItem))
                EnsureComp<UnremoveableComponent>(vItem.Value);
        }
    }

    private bool CanBlockHands(EntityUid wearer)
    {
        foreach (var hand in _hands.EnumerateHands(wearer))
        {
            if (!_hands.TryGetHeldItem(wearer, hand, out _))
                continue;

            if (!_hands.CanDropHeld(wearer, hand))
                return false;
        }

        return true;
    }

    private bool TryGetWearer(Entity<FoldedHandsClothingComponent> ent, out EntityUid wearer)
    {
        wearer = default;

        if (!TryComp<ClothingComponent>(ent, out var clothing) || clothing.InSlot == null)
            return false;

        if (!_container.TryGetContainingContainer(ent.Owner, out var cont))
            return false;

        wearer = cont.Owner;
        return true;
    }
}
