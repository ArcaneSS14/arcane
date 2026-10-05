using Content.Shared._Arcane.ERP;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server._Arcane.ERP;

public sealed class CondomSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedCondomSystem _shared = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    /// <summary>
    ///     Fills the condom worn by <paramref name="wearer"/>. Returns false when the wearer has none.
    /// </summary>
    public bool TryFill(EntityUid wearer)
    {
        if (!_shared.TryGetWorn(wearer, out var condom))
            return false;

        return TryFill(condom.Value);
    }

    /// <summary>
    ///     Adds an ejaculation's worth of cum to the condom. Returns false when it cannot hold any.
    /// </summary>
    public bool TryFill(Entity<CondomComponent> ent)
    {
        if (ent.Comp.Full)
            return false;

        if (!TryComp<SolutionContainerManagerComponent>(ent, out var manager))
            return false;

        if (!_solutionContainer.TryGetSolution((ent.Owner, (SolutionContainerManagerComponent?) manager), ent.Comp.SolutionId, out var container, out _))
            return false;

        var toAdd = new Solution([new ReagentQuantity(ent.Comp.CumReagent, ent.Comp.FillPerEjaculation)], false);
        if (_solutionContainer.AddSolution(container.Value, toAdd) <= FixedPoint2.Zero)
            return false;

        ent.Comp.Fill = container.Value.Comp.Solution.GetReagentQuantity(ent.Comp.CumReagent);
        Dirty(ent);

        if (ent.Comp.Fill >= ent.Comp.Capacity)
            BecomeFilled(ent);

        return true;
    }

    /// <summary>
    ///     Swaps this condom for the used-up prototype, carrying the cum over and keeping it where it was.
    /// </summary>
    private void BecomeFilled(Entity<CondomComponent> ent)
    {
        if (ent.Comp.Full || string.IsNullOrEmpty(ent.Comp.FilledPrototype.Id))
            return;

        // A condom is only ever filled while worn, so the only container that matters is the wearer's slot.
        EntityUid? wearer = null;
        string? slotId = null;

        if (_container.TryGetContainingContainer((ent.Owner, (TransformComponent?) null, null), out var container)
            && container is ContainerSlot slot
            && _inventory.TryUnequip(slot.Owner, slot.ID, out var removed, silent: true, force: true)
            && removed == ent.Owner)
        {
            wearer = slot.Owner;
            slotId = slot.ID;
        }

        var xform = Transform(ent);
        var filled = Spawn(ent.Comp.FilledPrototype, xform.Coordinates);
        _transform.SetLocalRotation(filled, xform.LocalRotation);

        // Move the cum over first so the worn item already carries the correct fill.
        TransferCum(ent, filled);

        // The wearer's underwear slot may still refuse the swap, leaving the used condom on the floor.
        if (wearer != null
            && slotId != null
            && !_inventory.TryEquip(wearer.Value, filled, slotId, silent: true, force: true))
        {
            _popup.PopupPredicted(Loc.GetString("condom-filled-dropped"), wearer.Value, wearer.Value, PopupType.MediumCaution);
        }

        QueueDel(ent);
    }

    private void TransferCum(Entity<CondomComponent> from, EntityUid to)
    {
        if (from.Comp.Fill <= FixedPoint2.Zero
            || !TryComp<CondomComponent>(to, out var toComp)
            || !TryComp<SolutionContainerManagerComponent>(to, out var manager)
            || !_solutionContainer.TryGetSolution((to, (SolutionContainerManagerComponent?) manager), toComp.SolutionId, out var solution, out _))
        {
            return;
        }

        _solutionContainer.AddSolution(solution.Value, new Solution([new ReagentQuantity(toComp.CumReagent, from.Comp.Fill)], false));
        toComp.Fill = from.Comp.Fill;
        Dirty(to, toComp);
    }
}
