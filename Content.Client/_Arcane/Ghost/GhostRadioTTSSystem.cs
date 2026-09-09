using Content.Client.Actions;
using Content.Client.Ghost;
using Content.Shared._Arcane.CCVars;
using Content.Shared._Arcane.Ghost;
using Content.Shared.Actions;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Client._Arcane.Ghost;

public sealed class GhostRadioTTSSystem : EntitySystem
{
    private const string ActionProtoId = "ActionToggleGhostRadioTTS";

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly GhostSystem _ghostSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionsSystem _actionsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostComponent, ToggleGhostRadioTTSActionEvent>(OnToggleGhostRadioTTS);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        _ghostSystem.PlayerDetached += OnPlayerDetached;
        _ghostSystem.PlayerRemoved += OnPlayerRemoved;
        _actionsSystem.OnActionAdded += OnActionAdded;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _ghostSystem.PlayerDetached -= OnPlayerDetached;
        _ghostSystem.PlayerRemoved -= OnPlayerRemoved;
        _actionsSystem.OnActionAdded -= OnActionAdded;
    }

    private void OnToggleGhostRadioTTS(EntityUid uid, GhostComponent component, ToggleGhostRadioTTSActionEvent args)
    {
        args.Handled = true;

        var enabled = !_cfg.GetCVar(ACCVars.TTSGhostRadioUseTTS);
        _cfg.SetCVar(ACCVars.TTSGhostRadioUseTTS, enabled);
        _actions.SetToggled((args.Action.Owner, args.Action.Comp), enabled);

        var locId = enabled ? "ghost-radio-tts-toggle-on" : "ghost-radio-tts-toggle-off";
        _popup.PopupEntity(Loc.GetString(locId), uid, uid);
    }

    private void OnActionAdded(EntityUid actionId)
    {
        if (!_ghostSystem.IsGhost || !IsGhostRadioTTSAction(actionId))
            return;

        _actions.SetToggled((actionId, null), _cfg.GetCVar(ACCVars.TTSGhostRadioUseTTS));
    }

    private bool IsGhostRadioTTSAction(EntityUid actionId)
    {
        return TryComp(actionId, out MetaDataComponent? meta)
        && meta.EntityPrototype?.ID == ActionProtoId;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        ResetGhostRadioTTS();
    }

    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent args)
    {
        if (HasComp<GhostComponent>(args.Entity))
            ResetGhostRadioTTS();
    }

    private void OnPlayerDetached()
    {
        ResetGhostRadioTTS();
    }

    private void OnPlayerRemoved(GhostComponent component)
    {
        ResetGhostRadioTTS();
    }

    private void ResetGhostRadioTTS()
    {
        _cfg.SetCVar(ACCVars.TTSGhostRadioUseTTS, true);
    }
}
