using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Ghost;
using Robust.Shared.GameObjects;

namespace Content.Server._Arcane.Ghost;

public sealed class GhostRadioTTSSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    private const string ToggleGhostRadioTTSAction = "ActionToggleGhostRadioTTS";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionsComponent, ComponentInit>(OnActionsComponentInit);
    }

    private void OnActionsComponentInit(EntityUid uid, ActionsComponent component, ComponentInit args)
    {
        if (!HasComp<GhostComponent>(uid))
            return;

        EntityUid? actionId = null;
        _actions.AddAction(uid, ref actionId, ToggleGhostRadioTTSAction);
    }
}