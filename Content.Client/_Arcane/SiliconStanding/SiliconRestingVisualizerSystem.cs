using Content.Shared._Arcane.SiliconStanding;
using Content.Shared.ActionBlocker;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Toggleable;
using Robust.Client.GameObjects;

namespace Content.Client._Arcane.SiliconStanding;

public sealed class SiliconRestingVisualizerSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedSiliconStandingSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconRestingComponent, ComponentStartup>(OnRestingStartup);
        SubscribeLocalEvent<SiliconRestingComponent, ComponentRemove>(OnRestingRemove);
    }

    private void OnRestingStartup(Entity<SiliconRestingComponent> ent, ref ComponentStartup args)
    {
        _actionBlocker.UpdateCanMove(ent.Owner);
        Refresh(ent.Owner, overrideResting: true);
    }

    private void OnRestingRemove(Entity<SiliconRestingComponent> ent, ref ComponentRemove args)
    {
        _actionBlocker.UpdateCanMove(ent.Owner);
        Refresh(ent.Owner, overrideResting: false);
    }

    public void Refresh(EntityUid uid, SpriteComponent? sprite = null, bool? overrideResting = null)
    {
        if (!Resolve(uid, ref sprite, false))
            return;

        if (!TryComp<BorgChassisComponent>(uid, out var borg))
            return;

        var isResting = overrideResting ?? GetRestingVisualState(uid);
        var spriteEnt = (uid, sprite);

        UpdateBorgBodyState(spriteEnt, isResting);

        if (!_appearance.TryGetData<bool>(uid, BorgVisuals.HasPlayer, out var hasPlayer))
            hasPlayer = false;

        var lightVisible = !isResting && (borg.BrainEntity != null || hasPlayer);
        _sprite.LayerSetVisible(spriteEnt, BorgVisualLayers.Light, lightVisible);

        if (_sprite.LayerMapTryGet(spriteEnt, BorgVisualLayers.LightStatus, out _, false))
        {
            var lightStatusVisible = false;
            if (!isResting)
                _appearance.TryGetData<bool>(uid, ToggleableVisuals.Enabled, out lightStatusVisible);

            _sprite.LayerSetVisible(spriteEnt, BorgVisualLayers.LightStatus, lightStatusVisible);
        }

        var lightState = hasPlayer ? borg.HasMindState : borg.NoMindState;
        _sprite.LayerSetRsiState(spriteEnt, BorgVisualLayers.Light, lightState);
    }

    private void UpdateBorgBodyState(Entity<SpriteComponent?> ent, bool isResting)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (!TryComp<SiliconRestingVisualsComponent>(ent, out var visuals))
            return;

        if (!_sprite.LayerMapTryGet(ent, BorgVisualLayers.Body, out var layer, false))
            return;

        var state = isResting ? visuals.RestBodyState : visuals.NormalBodyState;
        _sprite.LayerSetRsiState(ent, layer, state);
    }

    private bool GetRestingVisualState(EntityUid uid)
    {
        return _appearance.TryGetData<bool>(uid, SiliconStandingVisuals.Resting, out var resting)
            ? resting
            : _standing.GetEffectiveResting(uid);
    }
}
