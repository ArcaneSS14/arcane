using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.InclothingTools;

public sealed class InclothingToolsSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InclothingToolsComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<InclothingToolsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<InclothingToolsComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<InclothingToolsComponent, SelectInclothingToolEvent>(OnSelectAction);
        SubscribeLocalEvent<InclothingToolsComponent, InclothingToolsUiMessage>(OnUiMessage);

        SubscribeLocalEvent<RandomInclothingToolsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RandomInclothingToolsComponent, GetItemActionsEvent>(OnGetItemActions);
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
            EnsureComp<UnremoveableComponent>(tool);
            var attached = EnsureComp<AttachedToolsComponent>(tool);
            attached.AttachedUid = entity;

            comp.ToolsUids.Add(tool);
            _container.Insert(tool, comp.Container, xform);

            Dirty(tool, attached);
        }
        if (comp.ActionEntity != null)
            return;

        Dirty(entity.Owner, comp);

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

    private void OnSelectAction(Entity<InclothingToolsComponent> entity, ref SelectInclothingToolEvent args)
    {
        if (args.Handled)
            return;

        var comp = entity.Comp;

        if (comp.Container == null || comp.Container.Count == 0)
            return;

        args.Handled = true;

        _uiSystem.OpenUi(entity.Owner, SelectInclothingToolUiKey.Key, args.Performer);
    }

    private void OnUiMessage(Entity<InclothingToolsComponent> entity, ref InclothingToolsUiMessage args)
    {
        var comp = entity.Comp;

        if (!comp.ToolsUids.Contains(GetEntity(args.AttachedTool)))
            return;

        var attachedTool = GetEntity(args.AttachedTool);

        if (comp.Container.Contains(attachedTool))
        {
            var activeHand = _handSystem.GetActiveHand(entity.Owner);

            if (string.IsNullOrEmpty(activeHand))
                return;

            if (_handSystem.HandIsEmpty(entity.Owner, activeHand))
            {
                _handSystem.DoPickup(entity.Owner, activeHand, attachedTool);
                _popupSystem.PopupPredicted("Roland pick ups weapon!", entity.Owner, entity.Owner, PopupType.Medium);
                return;
            }

            if (!comp.ForseDropHeldItem || !_handSystem.TryForcePickup(entity.Owner, attachedTool, activeHand))
            {
                _popupSystem.PopupClient("Can't forse drop item", entity.Owner);
                return;
            }
        }
        else
        {
            _container.Insert(attachedTool, comp.Container);
        }
    }
}

public sealed partial class SelectInclothingToolEvent : InstantActionEvent { }

public sealed partial class RandomInclothingToolEvent : InstantActionEvent { }


[Serializable, NetSerializable]
public sealed partial class InclothingToolsUiMessage : BoundUserInterfaceMessage
{
    public NetEntity AttachedTool;

    public InclothingToolsUiMessage(NetEntity attachedTool)
    {
        AttachedTool = attachedTool;
    }
}

[Serializable, NetSerializable]
public enum SelectInclothingToolUiKey : byte
{
    Key
}