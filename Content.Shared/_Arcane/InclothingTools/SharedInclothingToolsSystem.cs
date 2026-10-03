using Content.Shared.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Arcane.InclothingTools;

public sealed class SharedInclothingToolsSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InclothingToolsComponent, ComponentInit>(OnCompInit);
        SubscribeLocalEvent<InclothingToolsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<InclothingToolsComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<InclothingToolsComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<InclothingToolsComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<InclothingToolsComponent, ActionSelectInclothingToolEvent>(OnSelectInclothingTool);
        SubscribeLocalEvent<InclothingToolsComponent, InclothingToolsUiMessage>(OnUiMessage);
        SubscribeLocalEvent<InclothingToolsComponent, InclothingToolsUnequipAllMessage>(OnUnequipAll);

        //SubscribeLocalEvent<RandomInclothingToolsComponent, ComponentInit>(OnCompInit);
        SubscribeLocalEvent<RandomInclothingToolsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RandomInclothingToolsComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<RandomInclothingToolsComponent, ActionRandomInclothingToolEvent>(OnRandomTool);
    }

    private void OnCompInit(Entity<InclothingToolsComponent> entity, ref ComponentInit args)
    {
        entity.Comp.Container = _container.EnsureContainer<Container>(entity, entity.Comp.ContainerId);
    }

    private void OnCompInit(Entity<RandomInclothingToolsComponent> entity, ref ComponentInit args)
    {

    }

    private void OnMapInit(Entity<InclothingToolsComponent> entity, ref MapInitEvent args)
    {
        if (entity.Comp.ToolPrototypes == null || entity.Comp.ToolPrototypes.Count == 0)
            return;

        if (!IsClientSide(entity))
        {
            foreach (var proto in entity.Comp.ToolPrototypes)
            {
                var spawnedTool = Spawn(proto);
                var attachedComp = AddComp<AttachedToolComponent>(spawnedTool);

                attachedComp.AttachedUid = entity;

                Dirty(spawnedTool, attachedComp);

                _container.Insert(spawnedTool, entity.Comp.Container);
                entity.Comp.ToolsUids.Add(spawnedTool);
            }
        }

        Dirty(entity, entity.Comp);

        if (_actionContainer.EnsureAction(entity, ref entity.Comp.ActionUid, entity.Comp.Action))
            _actionsSystem.SetEntityIcon(entity.Comp.ActionUid.Value, entity);
    }

    private void OnMapInit(Entity<RandomInclothingToolsComponent> entity, ref MapInitEvent args)
    {
        if (!HasComp<InclothingToolsComponent>(entity))
            return;

        if (_actionContainer.EnsureAction(entity, ref entity.Comp.ActionUid, entity.Comp.Action))
            _actionsSystem.SetEntityIcon(entity.Comp.ActionUid.Value, entity);
    }
    private void OnGetActions(Entity<InclothingToolsComponent> entity, ref GetItemActionsEvent args)
    {
        if (entity.Comp.ActionUid == null)
            return;

        args.AddAction(entity.Comp.ActionUid);
    }

    private void OnUnequipAttempt(Entity<InclothingToolsComponent> entity, ref BeingUnequippedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (entity.Comp.EquippedTools <= 0)
            return;

        if (_mobState.IsAlive(args.UnEquipTarget))
            return;

        _popups.PopupClient(Loc.GetString("inclothing-tools-unequip-tools-first"), args.Unequipee);
    }

    private void OnGotUnequipped(Entity<InclothingToolsComponent> entity, ref GotUnequippedEvent args)
    {
        if (entity.Comp.EquippedTools <= 0)
            return;

        foreach (var tool in entity.Comp.ToolsUids)
        {
            if (!entity.Comp.Container.Contains(tool))
                Unequip(entity, tool);
        }
    }

    private void OnGetActions(Entity<RandomInclothingToolsComponent> entity, ref GetItemActionsEvent args)
    {
        if (entity.Comp.ActionUid == null)
            return;

        args.AddAction(entity.Comp.ActionUid);
    }

    private void OnSelectInclothingTool(Entity<InclothingToolsComponent> entity, ref ActionSelectInclothingToolEvent args)
    {
        if (entity.Comp.Container == null || entity.Comp.Container.Count <= 0)
            return;

        _ui.OpenUi(entity.Owner, SelectInclothingToolUiKey.Key, args.Performer);
    }

    private void OnUiMessage(Entity<InclothingToolsComponent> entity, ref InclothingToolsUiMessage args)
    {
        var selectedTool = GetEntity(args.AttachedTool);

        if (!HasComp<AttachedToolComponent>(selectedTool))
            return;

        if (!entity.Comp.ToolsUids.Contains(selectedTool))
            return;

        if (!entity.Comp.Container.Contains(selectedTool))
            Unequip(entity, selectedTool);
        else
            TryEquipOrReplace(entity, selectedTool, args.Actor);
    }

    private void OnUnequipAll(Entity<InclothingToolsComponent> entity, ref InclothingToolsUnequipAllMessage args)
    {
        foreach (var tool in entity.Comp.ToolsUids)
        {
            if (!entity.Comp.Container.Contains(tool))
                Unequip(entity, tool);
        }
    }

    private void OnRandomTool(Entity<RandomInclothingToolsComponent> entity, ref ActionRandomInclothingToolEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<InclothingToolsComponent>(entity, out var clothing) || clothing == null)
            return;

        var uids = clothing.ToolsUids;

        if (uids.Count == 0)
            return;

        if (entity.Comp.RemainingTools.Count == 0)
            RefillRandomQueue(entity, clothing);

        if (entity.Comp.RemainingTools.Count == 0)
            return;

        var selectedTool = entity.Comp.RemainingTools[0];

        TryEquipOrReplace((entity.Owner, clothing), selectedTool, args.Performer);

        entity.Comp.RemainingTools.RemoveAt(0);

        Dirty(entity.Owner, clothing);
        Dirty(entity);
    }

    public void RefillRandomQueue(Entity<RandomInclothingToolsComponent> entity, InclothingToolsComponent? clothing = null)
    {
        entity.Comp.RemainingTools.Clear();

        if (clothing == null)
        {
            if (!TryComp(entity, out clothing) || clothing == null)
                return;
        }

        foreach (var tool in clothing.ToolsUids)
        {
            entity.Comp.RemainingTools.Add(tool);
        }

        var rand = new System.Random((int) _timing.CurTick.Value);

        rand.Shuffle(entity.Comp.RemainingTools);
    }

    public bool TryEquipOrReplace(Entity<InclothingToolsComponent> entity, EntityUid tool, EntityUid actor)
    {
        var activeHand = _handsSystem.GetActiveHand(actor);

        if (string.IsNullOrEmpty(activeHand))
            return false;

        if (_handsSystem.HandIsEmpty(actor, activeHand))
        {
            Equip(entity, tool, actor);
            return true;
        }

        var itemInHand = _handsSystem.GetHeldItem(actor, activeHand);

        if (TryComp<AttachedToolComponent>(itemInHand, out var attached))
        {
            if (attached.AttachedUid == entity.Owner)
            {
                Replaсe(entity, itemInHand!.Value, tool, actor, activeHand);
                return true;
            }
        }

        if (entity.Comp.ForseDropHeldItem)
        {
            if (_handsSystem.TryDrop(itemInHand!.Value))
            {
                Equip(entity, tool, actor);
                return true;
            }
        }

        _popups.PopupClient(Loc.GetString("inclothing-tools-equip-failed"), actor);
        return false;
    }

    private void Equip(Entity<InclothingToolsComponent> entity, EntityUid tool, EntityUid actor, string? hand = null)
    {
        if (entity.Comp.MaxEquippedTools <= entity.Comp.EquippedTools)
        {
            _popups.PopupClient(Loc.GetString("inclothing-tools-max-equipped"), actor);
            return;
        }

        string? activeHand;

        if (hand == null)
            activeHand = _handsSystem.GetActiveHand(actor);
        else
            activeHand = hand;

        if (string.IsNullOrEmpty(activeHand))
            return;

        if (!_handsSystem.TryForcePickup(actor, tool, activeHand))
            return;

        AddComp<UnremoveableComponent>(tool);
        entity.Comp.EquippedTools++;
    }

    private void Unequip(Entity<InclothingToolsComponent> entity, EntityUid tool)
    {
        RemComp<UnremoveableComponent>(tool);
        _container.Insert(tool, entity.Comp.Container);
        entity.Comp.EquippedTools--;
    }

    private void Replaсe(Entity<InclothingToolsComponent> entity, EntityUid replacedTool, EntityUid selectedTool, EntityUid actor, string? hand = null)
    {
        Unequip(entity, replacedTool);
        Equip(entity, selectedTool, actor, hand);
    }
}