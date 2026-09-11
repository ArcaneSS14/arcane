using Content.Shared.Actions;
using Content.Shared.Clothing;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.InclothingTools;

public sealed class InclothingToolsSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InclothingToolsComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<InclothingToolsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<InclothingToolsComponent, GetItemActionsEvent>(OnGetItemActions);

        SubscribeLocalEvent<RandomInclothingToolsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RandomInclothingToolsComponent, GetItemActionsEvent>(OnGetItemActions);

        SubscribeLocalEvent<InclothingToolsComponent, ClothingDidEquippedEvent>(OnEquipped);
    }

    private void OnComponentInit(Entity<InclothingToolsComponent> entity, ref ComponentInit args)
    {
        var comp = entity.Comp;

        comp.Container = _container.EnsureContainer<Container>(entity, comp.ContainerId);
    }

    private void OnMapInit(Entity<InclothingToolsComponent> entity, ref MapInitEvent args)
    {
        var comp = entity.Comp;

        if (comp.Container.Count != 0)
        {
            DebugTools.Assert(comp.Container.Count != 0, "Unexpected entity present inside of a inclothing tools container.");
            return;
        }

        if (comp.ToolPrototypes == null)
            return;

        var xform = Transform(entity);
        var prototypes = comp.ToolPrototypes;

        foreach (var proto in prototypes)
        {
            var tool = Spawn(proto, xform.Coordinates);
            var attached = EnsureComp<AttachedToolsComponent>(tool);
            attached.AttachedUid = entity;

            comp.ToolsUids.Add(tool);
            _container.Insert(tool, comp.Container, xform);

            Dirty(tool, attached);
        }
        if (comp.ActionEntity != null)
            return;

        if (_actionContainer.EnsureAction(entity, ref comp.ActionEntity, out var _, comp.Action))
            _actions.SetEntityIcon(comp.ActionEntity.Value, entity);
    }

    private void OnMapInit(Entity<RandomInclothingToolsComponent> entity, ref MapInitEvent args)
    {
        if (entity.Comp.ActionEntity != null)
            return;

        if (_actionContainer.EnsureAction(entity, ref entity.Comp.ActionEntity, out var _, entity.Comp.Action))
            _actions.SetEntityIcon(entity.Comp.ActionEntity.Value, entity);
    }

    private void OnGetItemActions(Entity<InclothingToolsComponent> entity, ref GetItemActionsEvent args)
    {
        if (entity.Comp.DisableAction || entity.Comp.ActionEntity == null)
            return;

        args.AddAction(entity.Comp.ActionEntity.Value);
    }

    private void OnGetItemActions(Entity<RandomInclothingToolsComponent> entity, ref GetItemActionsEvent args)
    {
        if (entity.Comp.ActionEntity == null)
            return;

        args.AddAction(entity.Comp.ActionEntity.Value);
    }

    private void OnEquipped(Entity<InclothingToolsComponent> entity, ref ClothingDidEquippedEvent args)
    {

    }
}

public sealed partial class SelectInclothingToolEvent : InstantActionEvent { }

public sealed partial class RandomInclothingToolEvent : InstantActionEvent { }