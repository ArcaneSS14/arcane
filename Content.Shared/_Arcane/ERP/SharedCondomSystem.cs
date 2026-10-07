using System.Diagnostics.CodeAnalysis;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Arcane.ERP;

public sealed class SharedCondomSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    /// <summary>
    ///     Sexes that can make a condom worth wearing.
    /// </summary>
    private static readonly HashSet<Sex> AllowedSexes = [Sex.Male, Sex.Futanari];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CondomComponent, BeingEquippedAttemptEvent>(OnEquipAttempt);
    }

    private void OnEquipAttempt(Entity<CondomComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        // Only applies when actually worn, not when just stowed in a pocket.
        if (TryComp<ClothingComponent>(ent, out var clothing) && (clothing.Slots & args.SlotFlags) == SlotFlags.NONE)
            return;

        if (TryComp<HumanoidAppearanceComponent>(args.EquipTarget, out var humanoid) && AllowedSexes.Contains(humanoid.Sex))
            return;

        args.Cancel();
        args.Reason = Loc.GetString("condom-wrong-sex");
    }

    /// <summary>
    ///     Gets the condom worn in the underwear slot, if any.
    /// </summary>
    public bool TryGetWorn(EntityUid wearer, [NotNullWhen(true)] out Entity<CondomComponent>? condom)
    {
        condom = null;

        if (!_inventory.TryGetContainerSlotEnumerator(wearer, out var slots, SlotFlags.UNDERWEAR))
            return false;

        while (slots.MoveNext(out var containerSlot))
        {
            if (containerSlot?.ContainedEntity is not { } item)
                continue;

            if (!TryComp<CondomComponent>(item, out var comp))
                continue;

            condom = (item, comp);
            return true;
        }

        return false;
    }
}
