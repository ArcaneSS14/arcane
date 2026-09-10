using Content.Shared.Actions;
using Content.Shared.Clothing;

namespace Content.Shared._Arcane.InclothingTools;

public sealed class InclothingToolsSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;

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

    }

    private void OnMapInit(Entity<InclothingToolsComponent> entity, ref MapInitEvent args)
    {
        if (entity.Comp.ActionEntity != null)
            return;

        if (_actionContainer.EnsureAction(entity, ref entity.Comp.ActionEntity, out var _, entity.Comp.Action))
            _actionsSystem.SetEntityIcon(entity.Comp.ActionEntity.Value, entity.Owner);
    }

    private void OnMapInit(Entity<RandomInclothingToolsComponent> entity, ref MapInitEvent args)
    {
        if (entity.Comp.ActionEntity != null)
            return;

        if (_actionContainer.EnsureAction(entity, ref entity.Comp.ActionEntity, out var _, entity.Comp.Action))
            _actionsSystem.SetEntityIcon(entity.Comp.ActionEntity.Value, entity.Owner);
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