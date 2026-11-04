// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.StatusIcon;
using Content.Client.UserInterface.Systems;
using Content.Shared.Damage;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Mech.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using static Robust.Shared.Maths.Color;

namespace Content.Client.Overlays;

/// <summary>
/// Overlay that shows a health bar on mobs.
/// </summary>
public sealed class EntityHealthBarOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly IPrototypeManager _prototype;
    private readonly IGameTiming _timing; // Arcane

    private readonly SharedTransformSystem _transform;
    private readonly MobStateSystem _mobStateSystem;
    private readonly MobThresholdSystem _mobThresholdSystem;
    private readonly StatusIconSystem _statusIconSystem;
    private readonly SpriteSystem _spriteSystem;
    private readonly ProgressColorSystem _progressColor;


    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public HashSet<string> DamageContainers = new();
    public ProtoId<HealthIconPrototype>? StatusIcon;

    // Arcane-Edit-Start: Vital damage walks the whole body of the mob, which is too much to do every frame.
    private const int MaxCachedProgress = 1024;
    private static readonly TimeSpan ProgressUpdateInterval = TimeSpan.FromSeconds(0.25);
    private readonly Dictionary<EntityUid, (TimeSpan NextUpdate, (float ratio, bool inCrit)? Progress)> _cachedProgress = new();
    // Arcane-Edit-End

    public EntityHealthBarOverlay(IEntityManager entManager, IPrototypeManager prototype, IGameTiming timing) // Arcane-Edit
    {
        _entManager = entManager;
        _prototype = prototype;
        _timing = timing; // Arcane
        _transform = _entManager.System<SharedTransformSystem>();
        _mobStateSystem = _entManager.System<MobStateSystem>();
        _mobThresholdSystem = _entManager.System<MobThresholdSystem>();
        _statusIconSystem = _entManager.System<StatusIconSystem>();
        _spriteSystem = _entManager.System<SpriteSystem>();
        _progressColor = _entManager.System<ProgressColorSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();

        const float scale = 1f;
        var scaleMatrix = Matrix3Helpers.CreateScale(new Vector2(scale, scale));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-rotation);
        _prototype.Resolve(StatusIcon, out var statusIcon);

        // Arcane-Edit-Start: Damageable alone matches every wall and item the client has ever seen, so the cost of
        // this overlay grew throughout the round. Only mobs and mechs can have a health bar.
        var mapId = args.MapId;
        var worldAabb = args.WorldAABB;

        void DrawBar(EntityUid uid, DamageableComponent damageableComponent, SpriteComponent spriteComponent)
        {
            if (statusIcon != null && !_statusIconSystem.IsVisible((uid, _entManager.GetComponent<MetaDataComponent>(uid)), statusIcon))
                return;

            // We want the stealth user to still be able to see his health bar himself
            if (!xformQuery.TryGetComponent(uid, out var xform) ||
                xform.MapID != mapId)
                return;

            if (damageableComponent.DamageContainerID == null || !DamageContainers.Contains(damageableComponent.DamageContainerID))
                return;

            // we use the status icon component bounds if specified otherwise use sprite
            var bounds = _entManager.GetComponentOrNull<StatusIconComponent>(uid)?.Bounds ?? _spriteSystem.GetLocalBounds((uid, spriteComponent));
            var worldPos = _transform.GetWorldPosition(xform, xformQuery);

            if (!bounds.Translated(worldPos).Intersects(worldAabb))
                return;

            // we are all progressing towards death every day
            // Arcane-Edit-Start
            (float ratio, bool inCrit)? progress;
            if (_entManager.TryGetComponent<MobStateComponent>(uid, out var mobState) &&
                _entManager.TryGetComponent<MobThresholdsComponent>(uid, out var mobThresholds))
            {
                progress = GetCachedProgress(uid, mobState, damageableComponent, mobThresholds); // Arcane-Edit
            }
            else if (_entManager.TryGetComponent<MechComponent>(uid, out var mech) && mech.MaxIntegrity > 0)
            {
                progress = (System.Math.Clamp((mech.Integrity / mech.MaxIntegrity).Float(), 0f, 1f), mech.Broken);
            }
            else
            {
                return;
            }

            if (progress is not { } healthProgress)
            // Arcane-Edit-End
                return;

            var worldPosition = _transform.GetWorldPosition(xform);
            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPosition);

            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty = Matrix3x2.Multiply(rotationMatrix, scaledWorld);

            handle.SetTransform(matty);

            var yOffset = bounds.Height * EyeManager.PixelsPerMeter / 2 - 3f;
            var widthOfMob = bounds.Width * EyeManager.PixelsPerMeter;

            var position = new Vector2(-widthOfMob / EyeManager.PixelsPerMeter / 2, yOffset / EyeManager.PixelsPerMeter);
            var color = GetProgressColor(healthProgress.ratio, healthProgress.inCrit); // Arcane-Edit

            // Hardcoded width of the progress bar because it doesn't match the texture.
            const float startX = 8f;
            var endX = widthOfMob - 8f;

            var xProgress = (endX - startX) * healthProgress.ratio + startX; // Arcane-Edit

            var boxBackground = new Box2(new Vector2(startX, 0f) / EyeManager.PixelsPerMeter, new Vector2(endX, 3f) / EyeManager.PixelsPerMeter);
            boxBackground = boxBackground.Translated(position);
            handle.DrawRect(boxBackground, Black.WithAlpha(192));

            var boxMain = new Box2(new Vector2(startX, 0f) / EyeManager.PixelsPerMeter, new Vector2(xProgress, 3f) / EyeManager.PixelsPerMeter);
            boxMain = boxMain.Translated(position);
            handle.DrawRect(boxMain, color);

            var pixelDarken = new Box2(new Vector2(startX, 2f) / EyeManager.PixelsPerMeter, new Vector2(xProgress, 3f) / EyeManager.PixelsPerMeter);
            pixelDarken = pixelDarken.Translated(position);
            handle.DrawRect(pixelDarken, Black.WithAlpha(128));
        }

        // Arcane-Start
        var mobs = _entManager.AllEntityQueryEnumerator<MobThresholdsComponent, MobStateComponent, DamageableComponent, SpriteComponent>();
        while (mobs.MoveNext(out var mobUid, out _, out _, out var mobDamageable, out var mobSprite))
        {
            DrawBar(mobUid, mobDamageable, mobSprite);
        }

        var mechs = _entManager.AllEntityQueryEnumerator<MechComponent, DamageableComponent, SpriteComponent>();
        while (mechs.MoveNext(out var mechUid, out _, out var mechDamageable, out var mechSprite))
        {
            // Mobs were already drawn by the query above.
            if (!_entManager.HasComponent<MobThresholdsComponent>(mechUid) || !_entManager.HasComponent<MobStateComponent>(mechUid))
                DrawBar(mechUid, mechDamageable, mechSprite);
        }
        // Arcane-End

        handle.SetTransform(Matrix3x2.Identity);
    }

    // Arcane-Start
    private (float ratio, bool inCrit)? GetCachedProgress(
        EntityUid uid,
        MobStateComponent component,
        DamageableComponent dmg,
        MobThresholdsComponent thresholds)
    {
        var now = _timing.RealTime;
        if (_cachedProgress.TryGetValue(uid, out var cached) && now < cached.NextUpdate)
            return cached.Progress;

        // Deleted mobs are never removed individually, so this also keeps the cache from growing forever.
        if (_cachedProgress.Count >= MaxCachedProgress)
            _cachedProgress.Clear();

        var progress = CalcProgress(uid, component, dmg, thresholds);
        _cachedProgress[uid] = (now + ProgressUpdateInterval, progress);
        return progress;
    }
    // Arcane-End

    /// <summary>
    /// Returns a ratio between 0 and 1, and whether the entity is in crit.
    /// </summary>
    private (float ratio, bool inCrit)? CalcProgress(EntityUid uid, MobStateComponent component, DamageableComponent dmg, MobThresholdsComponent thresholds)
    {
        var totalDamage = _mobThresholdSystem.CheckVitalDamage(uid, dmg); // GoobStation
        if (_mobStateSystem.IsAlive(uid, component))
        {
            if (dmg.HealthBarThreshold != null && totalDamage < dmg.HealthBarThreshold) // GoobStation
                return null;

            if (!_mobThresholdSystem.TryGetThresholdForState(uid, MobState.SoftCritical, out var threshold, thresholds) && // Orion-Edit
                !_mobThresholdSystem.TryGetThresholdForState(uid, MobState.Dead, out threshold, thresholds))
                return (1, false);

            var ratio = 1 - ((FixedPoint2)(totalDamage / threshold)).Float(); // GoobStation
            return (ratio, false);
        }

        if (_mobStateSystem.IsCritical(uid, component))
        {
            if (!_mobThresholdSystem.TryGetThresholdForState(uid, MobState.SoftCritical, out var critThreshold, thresholds) || // Orion-Edit
                !_mobThresholdSystem.TryGetThresholdForState(uid, MobState.Dead, out var deadThreshold, thresholds))
            {
                return (1, true);
            }

            var ratio = 1 - ((totalDamage - critThreshold) / (deadThreshold - critThreshold)).Value.Float(); // GoobStation

            return (ratio, true);
        }

        return (0, true);
    }

    public Color GetProgressColor(float progress, bool crit)
    {
        if (crit)
            progress = 0;

        return _progressColor.GetProgressColor(progress);
    }
}
