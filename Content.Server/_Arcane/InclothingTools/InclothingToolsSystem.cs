/*using System.Linq;
using Content.Server.Popups;
using Content.Shared._Arcane.InclothingTools;
using Content.Shared.Actions;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Random;

namespace Content.Server._Arcane.InclothingTools;

public sealed class InclothingToolsSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedInclothingToolsSystem _inclothingTools = default!;

    public override void Initialize()
    {
        base.Initialize();

        // SubscribeLocalEvent<InclothingToolsComponent, ActionRandomInclothingToolEvent>(OnRandomTool);
    }

    private void OnRandomTool(Entity<InclothingToolsComponent> entity, ref ActionRandomInclothingToolEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var selectedTool = _random.GetItems(entity.Comp.Container.ContainedEntities.ToList(), 1)[0];
        _inclothingTools.TryEquipOrReplace(entity, selectedTool, args.Performer);

        Dirty(entity);
    }
}*/