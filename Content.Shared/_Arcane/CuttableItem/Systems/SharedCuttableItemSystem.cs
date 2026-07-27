using Content.Shared._Arcane.CuttableItem.Components;
using Content.Shared.Interaction;
using Content.Shared.Tools;
using Content.Shared.DoAfter;
using Content.Shared.CuttableItem;
using Content.Shared.Popups;
using Content.Shared.Inventory;
using Content.Shared.Tools.Systems;
using Robust.Shared.Network;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.CuttableItem.Systems;

public sealed class SharedCuttableItemSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CuttableItemComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CuttableItemComponent, CuttableDoAfterEvent>(OnCutCompleted);
        SubscribeLocalEvent<CuttableItemComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractUsing(EntityUid uid, CuttableItemComponent comp, InteractUsingEvent args)
    {
        var toolFound = false;

        foreach (var quality in comp.ToolQualities)
        {
            if (_toolSystem.HasQuality(args.Used, quality))
            {
                toolFound = true;
                break;
            }
        }

        if (_netManager.IsClient || !toolFound)
            return;

        args.Handled = true;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, comp.Delay, new CuttableDoAfterEvent(), uid, target: uid, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _popup.PopupEntity(Loc.GetString("cuttable-item-attempt-broken-popup", ("item", uid), ("user", Loc.GetString(Name(args.User)))), uid);

        _doAfter.TryStartDoAfter(doAfterArgs);
        Dirty(uid, comp);
    }

    private void OnCutCompleted(EntityUid uid, CuttableItemComponent comp, CuttableDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || _netManager.IsClient)
            return;

        args.Handled = true;

        var victim = Transform(uid).ParentUid;

        if (victim.IsValid())
        {
            if (_inventorySystem.TryGetSlots(victim, out var slotDefinitions))
            {
                if (_netManager.IsClient)
                    return;

                foreach (var slotDef in slotDefinitions)
                {
                    if (_inventorySystem.TryGetSlotEntity(victim, slotDef.Name, out var slotEntity) && slotEntity == uid)
                    {
                        var target = args.User;

                        if (_inventorySystem.TryUnequip(target, victim, slotDef.Name, force: true))
                        {
                            _transformSystem.AttachToGridOrMap(uid);

                            var victimCoords = Transform(victim).Coordinates;
                            _transformSystem.SetCoordinates(uid, victimCoords);
                            _popup.PopupEntity(Loc.GetString("cuttable-item-broken-moment-popup", ("item", uid)), uid);

                            var ev = new CuttableCutEvent(target, uid);
                            RaiseLocalEvent(uid, ev);
                        }
                        break;
                    }
                }
            }
        }
    }

    private void OnExamined(EntityUid uid, CuttableItemComponent comp, ref ExaminedEvent args)
    {
        if (comp.ToolQualities.Count == 0)
            return;

        var message = new FormattedMessage();
        message.AddMarkupOrThrow(Loc.GetString("cuttable-item-examine-header") + "\n");

        foreach (var qualityId in comp.ToolQualities)
        {
            if (_prototypeManager.TryIndex<ToolQualityPrototype>(qualityId, out var qualityProto))
            {
                var qualityName = Loc.GetString(qualityProto.Name);
                message.AddMarkupOrThrow($" - [color=yellow]{qualityName}[/color]\n");
            }
        }
        args.PushMessage(message);
    }
}
