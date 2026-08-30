// SPDX-License-Identifier: MIT

using Content.Client.Hands.Systems;
using Content.Client.NPC.HTN;
using Content.Shared.CCVar;
using Content.Shared.CombatMode;
using Content.Shared._Arcane.CCVars;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client.CombatMode;

public sealed class CombatModeSystem : SharedCombatModeSystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    /// <summary>
    /// Raised whenever combat mode changes.
    /// </summary>
    public event Action<bool>? LocalPlayerCombatModeUpdated;

    // Orion-Start
    private EntityQuery<SpriteComponent> _spriteQuery;
    private bool _combatModeSoundEnabled;
    // Orion-End

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CombatModeComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<CombatModeComponent, GetStatusIconsEvent>(UpdateCombatModeIndicator); // Orion
        Subs.CVar(_cfg, CCVars.CombatModeIndicatorsPointShow, OnShowCombatIndicatorsChanged, true);
        Subs.CVar(_cfg, CCVars.CombatIndicator, (bool value) => OnShowCombatIndicatorChanged(value), true); // Orion
        Subs.CVar(_cfg, ACCVars.CombatModeBlockItemPickup, OnBlockItemPickupChanged, true); // Arcane

        // Orion-Start
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _cfg.OnValueChanged(CCVars.CombatModeSoundEnabled, v => _combatModeSoundEnabled = v, true);
        // Orion-End
    }

    private void OnHandleState(EntityUid uid, CombatModeComponent component, ref AfterAutoHandleStateEvent args)
    {
        UpdateHud(uid);
    }

    public override void Shutdown()
    {
        _overlayManager.RemoveOverlay<CombatModeIndicatorsOverlay>();

        base.Shutdown();
    }

    public bool IsInCombatMode()
    {
        var entity = _playerManager.LocalEntity;

        if (entity == null)
            return false;

        return IsInCombatMode(entity.Value);
    }

    public override void SetInCombatMode(EntityUid entity, bool value, CombatModeComponent? component = null)
    {
        base.SetInCombatMode(entity, value, component);
        UpdateHud(entity);
    }

    protected override bool IsNpc(EntityUid uid)
    {
        return HasComp<HTNComponent>(uid);
    }

    private void UpdateHud(EntityUid entity)
    {
        if (entity != _playerManager.LocalEntity || !Timing.IsFirstTimePredicted)
        {
            return;
        }

        var inCombatMode = IsInCombatMode();
        TryPlayCombatModeSound(entity); // Orion
        LocalPlayerCombatModeUpdated?.Invoke(inCombatMode);
    }

    private void OnShowCombatIndicatorsChanged(bool isShow)
    {
        if (isShow)
        {
            _overlayManager.AddOverlay(new CombatModeIndicatorsOverlay(
                _inputManager,
                EntityManager,
                _eye,
                this,
                EntityManager.System<HandsSystem>()));
        }
        else
        {
            _overlayManager.RemoveOverlay<CombatModeIndicatorsOverlay>();
        }
    }

    // Orion-Start
    private bool _combatIndicatorEnabled = false;
    private EntityUid? _blockPickupSyncedFor; // Arcane

    private void OnShowCombatIndicatorChanged(bool value)
    {
        _combatIndicatorEnabled = value;
    }

    // Arcane-Start
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var entity = _playerManager.LocalEntity;
        if (entity == null || !TryComp<CombatModeComponent>(entity.Value, out var comp))
            return;

        // Only sync once per controlled entity; on respawn/transfer the LocalEntity changes and the
        // alternative interaction window setting has to be sent to the server for the new entity too.
        if (_blockPickupSyncedFor == entity.Value)
            return;

        _blockPickupSyncedFor = entity.Value;
        var value = _cfg.GetCVar(ACCVars.CombatModeBlockItemPickup);
        comp.BlockItemPickup = value;
        RaiseNetworkEvent(new CombatModeBlockItemPickupChangedMessage(value));
    }

    private void OnBlockItemPickupChanged(bool value)
    {
        var entity = _playerManager.LocalEntity;
        if (entity == null || !TryComp<CombatModeComponent>(entity.Value, out var comp))
            return;

        comp.BlockItemPickup = value;
        RaiseNetworkEvent(new CombatModeBlockItemPickupChangedMessage(value));
    }
    // Arcane-End

    private void UpdateCombatModeIndicator(EntityUid uid, CombatModeComponent comp, ref GetStatusIconsEvent _)
    {
        if (!_combatIndicatorEnabled)
        {
            if (_spriteQuery.TryComp(uid, out var sprite) && sprite.LayerMapTryGet("combat_mode_indicator", out var layerToRemove))
            {
                sprite.RemoveLayer(layerToRemove);
            }
            return;
        }

        if (comp.IsInCombatMode)
        {
            if (!_spriteQuery.TryComp(uid, out var sprite))
                return;

            if (!sprite.LayerMapTryGet("combat_mode_indicator", out var layer))
            {
                if (!_spriteQuery.TryComp(uid, out var sprite2))
                    return;

                layer = sprite2.AddLayer(new SpriteSpecifier.Rsi(new ResPath("_Orion/Effects/combat_mode.rsi"), "combat_mode"));
                sprite2.LayerMapSet("combat_mode_indicator", layer);
            }
        }
        else
        {
            if (_spriteQuery.TryComp(uid, out var sprite) && sprite.LayerMapTryGet("combat_mode_indicator", out var layerToRemove))
            {
                sprite.RemoveLayer(layerToRemove);
            }
        }
    }

    /// <summary>
    /// Plays sounds based on activation/deactivation of the CombatMode
    /// </summary>
    /// <param name="uid">uid of entity that'll play the sound</param>
    private void TryPlayCombatModeSound(EntityUid uid)
    {
        if (!_combatModeSoundEnabled)
            return;

        if (!TryComp<CombatModeComponent>(uid, out var comp))
            return;

        var inCombatMode = IsInCombatMode();

        switch (inCombatMode)
        {
            case true:
                if (comp.CombatActivationSound == null)
                    return;
                _audio.PlayLocal(comp.CombatActivationSound, uid, uid);
                break;

            case false:
                if (comp.CombatDeactivationSound == null)
                    return;
                _audio.PlayLocal(comp.CombatDeactivationSound, uid, uid);
                break;
        }
    }
    // Orion-End
}
