// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Orion.Mood;
using Content.Shared.Alert;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusIcon;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Nutrition.EntitySystems;

public sealed class HungerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private readonly SharedJetpackSystem _jetpack = default!;
    [Dependency] private readonly IConfigurationManager _config = default!; // Orion
    [Dependency] private readonly INetManager _net = default!; // Orion

    private static readonly ProtoId<SatiationIconPrototype> HungerIconOverfedId = "HungerIconOverfed";
    private static readonly ProtoId<SatiationIconPrototype> HungerIconPeckishId = "HungerIconPeckish";
    private static readonly ProtoId<SatiationIconPrototype> HungerIconStarvingId = "HungerIconStarving";

    // Orion-Start
    private static readonly HashSet<HungerThreshold> MovementAffectingThresholds = new()
    {
        HungerThreshold.Overfed,
        HungerThreshold.Okay,
        HungerThreshold.Peckish,
    };

    private SatiationIconPrototype? _overfedIcon;
    private SatiationIconPrototype? _peckishIcon;
    private SatiationIconPrototype? _starvingIcon;
    // Orion-End

    public override void Initialize()
    {
        base.Initialize();

        // Orion-Start
        _prototype.TryIndex(HungerIconOverfedId, out _overfedIcon);
        _prototype.TryIndex(HungerIconPeckishId, out _peckishIcon);
        _prototype.TryIndex(HungerIconStarvingId, out _starvingIcon);
        // Orion-End

        SubscribeLocalEvent<HungerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HungerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<HungerComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<HungerComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnMapInit(EntityUid uid, HungerComponent component, MapInitEvent args)
    {
        // <goobstation> Starting hunger override
        if (component.StartingHunger is not null)
        {
            SetHunger(uid, component.StartingHunger.Value, component);
            return;
        }
        // </goobstation>
        var amount = _random.Next(
            (int) component.Thresholds[HungerThreshold.Peckish] + 10,
            (int) component.Thresholds[HungerThreshold.Okay]);
        SetHunger(uid, amount, component);
    }

    private void OnShutdown(EntityUid uid, HungerComponent component, ComponentShutdown args)
    {
        _alerts.ClearAlertCategory(uid, component.HungerAlertCategory);
    }

    private void OnRefreshMovespeed(EntityUid uid, HungerComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        // Orion-Start
        if (_config.GetCVar(CCVars.MoodEnabled))
            return;
        // Orion-End

        if (component.CurrentThreshold > HungerThreshold.Starving)
            return;

        if (_jetpack.IsUserFlying(uid))
            return;

        args.ModifySpeed(component.StarvingSlowdownModifier, component.StarvingSlowdownModifier);
    }

    private void OnRejuvenate(EntityUid uid, HungerComponent component, RejuvenateEvent args)
    {
        SetHunger(uid, component.Thresholds[HungerThreshold.Okay], component);
    }

    /// <summary>
    /// Gets the current hunger value of the given <see cref="HungerComponent"/>.
    /// </summary>
    public float GetHunger(HungerComponent component)
    {
        var dt = _timing.CurTime - component.LastAuthoritativeHungerChangeTime;
        var value = component.LastAuthoritativeHungerValue - (float)dt.TotalSeconds * component.ActualDecayRate;
        return ClampHungerWithinThresholds(component, value);
    }

    /// <summary>
    /// Adds to the current hunger of an entity by the specified value
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="amount"></param>
    /// <param name="component"></param>
    public void ModifyHunger(EntityUid uid, float amount, HungerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;
        SetHunger(uid, GetHunger(component) + amount, component);
    }

    /// <summary>
    /// Sets the current hunger of an entity to the specified value
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="amount"></param>
    /// <param name="component"></param>
    public void SetHunger(EntityUid uid, float amount, HungerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        SetAuthoritativeHungerValue((uid, component), amount);
        UpdateCurrentThreshold(uid, component);
    }

    /// <summary>
    /// Sets <see cref="HungerComponent.LastAuthoritativeHungerValue"/> and
    /// <see cref="HungerComponent.LastAuthoritativeHungerChangeTime"/>, and dirties this entity. This "resets" the
    /// starting point for <see cref="GetHunger"/>'s calculation.
    /// </summary>
    /// <param name="entity">The entity whose hunger will be set.</param>
    /// <param name="value">The value to set the entity's hunger to.</param>
    private void SetAuthoritativeHungerValue(Entity<HungerComponent> entity, float value)
    {
        entity.Comp.LastAuthoritativeHungerChangeTime = _timing.CurTime;
        entity.Comp.LastAuthoritativeHungerValue = ClampHungerWithinThresholds(entity.Comp, value);
        DirtyField(entity.Owner, entity.Comp, nameof(HungerComponent.LastAuthoritativeHungerChangeTime));
        DirtyField(entity.Owner, entity.Comp, nameof(HungerComponent.LastAuthoritativeHungerValue));
    }

    private void UpdateCurrentThreshold(EntityUid uid, HungerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var calculatedHungerThreshold = GetHungerThreshold(component);
        if (calculatedHungerThreshold == component.CurrentThreshold)
            return;

        component.CurrentThreshold = calculatedHungerThreshold;
        DirtyField(uid, component, nameof(HungerComponent.CurrentThreshold));
        DoHungerThresholdEffects(uid, component);
    }

    private void DoHungerThresholdEffects(EntityUid uid, HungerComponent? component = null, bool force = false)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.CurrentThreshold == component.LastThreshold && !force)
            return;

        if (GetMovementThreshold(component.CurrentThreshold) != GetMovementThreshold(component.LastThreshold))
            _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);

        // Orion-Start
        if (_config.GetCVar(CCVars.MoodEnabled) && _net.IsServer)
            RaiseLocalEvent(uid, new MoodEffectEvent("Hunger" + component.CurrentThreshold));
        // Orion-End

        if (component.HungerThresholdAlerts.TryGetValue(component.CurrentThreshold, out var alertId))
        {
            _alerts.ShowAlert(uid, alertId);
        }
        else
        {
            _alerts.ClearAlertCategory(uid, component.HungerAlertCategory);
        }

        if (component.HungerThresholdDecayModifiers.TryGetValue(component.CurrentThreshold, out var modifier))
        {
            // Orion-Edit-Start
            var newDecayRate = component.BaseDecayRate * modifier;
            if (Math.Abs(component.ActualDecayRate - newDecayRate) > 0.001f)
            {
                var currentHunger = GetHunger(component);
                component.ActualDecayRate = newDecayRate;
                DirtyField(uid, component, nameof(HungerComponent.ActualDecayRate));
                SetAuthoritativeHungerValue((uid, component), currentHunger);
            }
            // Orion-Edit-End
        }

        component.LastThreshold = component.CurrentThreshold;
        DirtyField(uid, component, nameof(HungerComponent.LastThreshold));
    }

    private void DoContinuousHungerEffects(EntityUid uid, HungerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.CurrentThreshold <= HungerThreshold.Starving &&
            component.StarvationDamage is { } damage &&
            !_mobState.IsDead(uid))
        {
            _damageable.TryChangeDamage(uid, damage, true, false);
        }
    }

    /// <summary>
    /// Gets the hunger threshold for an entity based on the amount of food specified.
    /// If a specific amount isn't specified, just uses the current hunger of the entity
    /// </summary>
    /// <param name="component"></param>
    /// <param name="food"></param>
    /// <returns></returns>
    public HungerThreshold GetHungerThreshold(HungerComponent component, float? food = null)
    {
        food ??= GetHunger(component);
        var result = HungerThreshold.Dead;
        var value = component.Thresholds[HungerThreshold.Overfed];
        foreach (var threshold in component.Thresholds)
        {
            if (threshold.Value <= value && threshold.Value >= food)
            {
                result = threshold.Key;
                value = threshold.Value;
            }
        }

        return result;
    }

    /// <summary>
    /// A check that returns if the entity is below a hunger threshold.
    /// </summary>
    public bool IsHungerBelowState(EntityUid uid, HungerThreshold threshold, float? food = null, HungerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false; // It's never going to go hungry, so it's probably fine to assume that it's not... you know, hungry.

        return GetHungerThreshold(comp, food) < threshold;
    }

    // Orion-Edit-Start
    private static bool GetMovementThreshold(HungerThreshold threshold)
    {
        return MovementAffectingThresholds.Contains(threshold);
    }
    // Orion-Edit-End

    public bool TryGetStatusIconPrototype(HungerComponent component, [NotNullWhen(true)] out SatiationIconPrototype? prototype)
    {
        // Orion-Edit-Start
        prototype = component.CurrentThreshold switch
        {
            HungerThreshold.Overfed => _overfedIcon,
            HungerThreshold.Peckish => _peckishIcon,
            HungerThreshold.Starving => _starvingIcon,
            _ => null,
        };
        // Orion-Edit-End

        return prototype != null;
    }

    private static float ClampHungerWithinThresholds(HungerComponent component, float hungerValue)
    {
        return Math.Clamp(hungerValue,
            component.Thresholds[HungerThreshold.Dead],
            component.Thresholds[HungerThreshold.Overfed]);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HungerComponent>();
        while (query.MoveNext(out var uid, out var hunger))
        {
            if (_timing.CurTime < hunger.NextThresholdUpdateTime)
                continue;
            hunger.NextThresholdUpdateTime = _timing.CurTime + hunger.ThresholdUpdateRate;

//            UpdateCurrentThreshold(uid, hunger); // Orion-Edit

            // Orion-Start
            if (_mobState.IsDead(uid))
                continue;
            // Orion-End

            UpdateCurrentThreshold(uid, hunger); // Orion
            DoContinuousHungerEffects(uid, hunger);
        }
    }
}
