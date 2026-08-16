using Content.Shared._Arcane.CuttableItem.Components;
using Content.Shared.Interaction;
using Content.Shared.DoAfter;
using Content.Shared.CuttableItem;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Shared.Network;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.CuttableItem.Systems;

public sealed partial class SharedCuttableItemSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedToolSystem _toolSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CuttableItemComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CuttableItemComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractUsing(EntityUid uid, CuttableItemComponent comp, InteractUsingEvent args)
    {
        if (args.Handled || _netManager.IsClient)
            return;

        var toolFound = false;
        for (var i = 0; i < comp.ToolQualities.Count; i++)
        {
            if (_toolSystem.HasQuality(args.Used, comp.ToolQualities[i]))
            {
                toolFound = true;
                break;
            }
        }

        if (!toolFound)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(comp.Delay), new CuttableDoAfterEvent(), uid, target: uid, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("cuttable-item-attempt-broken-popup", ("item", uid), ("user", Name(args.User))), uid);
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
            if (_prototypeManager.TryIndex(qualityId, out var qualityProto))
            {
                var qualityName = Loc.GetString(qualityProto.Name);
                message.AddMarkupOrThrow($" - [color=yellow]{qualityName}[/color]\n");
            }
        }
        args.PushMessage(message);
    }
}
