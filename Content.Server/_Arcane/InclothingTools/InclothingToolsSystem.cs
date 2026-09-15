using System.Linq;
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

    private const string ActionRandomToolId = "ActionRandomInclothingTool";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RandomInclothingToolEvent>(OnRandomTool);
    }

    private void OnRandomTool(RandomInclothingToolEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { Valid: true } user)
            return;

        if (!_actions.TryGetActionById(user, ActionRandomToolId, out var action) || action == null)
        {
            Log.Warning($"Player {args.SenderSession.Name} tried to activate {ActionRandomToolId} without having it.");
            return;
        }

        if (_actions.IsCooldownActive(action.Value))
        {
            _popup.PopupEntity(Loc.GetString("inclothing-tools-cooldown"), user, user);
            return;
        }

        if (!_mobState.IsAlive(user))
            return;

        if (!TryGetEntity(ev.Clothing, out var clothing) || clothing is not { Valid: true })
            return;

        if (!TryComp<InclothingToolsComponent>(clothing, out var inclothingTools) || inclothingTools == null)
            return;

        _actions.StartUseDelay(action.Value.Owner);

        var selectedTool = _random.GetItems(inclothingTools.Container.ContainedEntities.ToList(), 1)[0];
        _inclothingTools.TryEquipOrReplace((clothing.Value, inclothingTools), selectedTool, user);

        Dirty(clothing.Value, inclothingTools);
    }
}