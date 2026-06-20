using Content.Shared._Arcane.Leash.Components;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Shared._Arcane.Leash.Systems;

public sealed partial class SharedCollarSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CollarComponent, AfterInteractEvent>(OnCollarAfterInteract, before: [typeof(IngestionSystem)]);
    }

    private void OnCollarAfterInteract(EntityUid uid, CollarComponent component, ref AfterInteractEvent args)
    {
        if (args.Handled ||
            !args.CanReach ||
            args.Target is not { Valid: true } target ||
            !TryComp<InventoryComponent>(target, out var inventory))
        {
            return;
        }

        if (_inventory.TryEquip(
            args.User,
            target,
            uid,
            "neck",
            predicted: true,
            inventory: inventory,
            checkDoafter: true,
            triggerHandContact: false))
        {
            args.Handled = true;
        }
    }
}
