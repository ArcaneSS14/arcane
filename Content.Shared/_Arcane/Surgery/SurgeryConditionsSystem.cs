using Content.Shared._Arcane.CCVars;
using Content.Shared._Arcane.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Drunk;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Arcane.Surgery;

/// <summary>
///     Where and in what state a surgery happens: these slow a step down, and a step slowed past the threshold can be botched.
///     Also checks surgeries that need a dead patient.
/// </summary>
public sealed partial class SurgeryConditionsSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    private const float MaxDrunkSeconds = 100f;

    private EntityQuery<BuckleComponent> _buckleQuery;
    private EntityQuery<SurgerySurfaceComponent> _surfaceQuery;

    private float _failureThreshold;
    private float _failureChancePerSecond;
    private float _selfSurgeryPenalty;
    private float _selfSurgeryFailureChance;
    private float _floorSpeedModifier;
    private float _anesthesiaSpeedModifier;
    private float _analyzedSpeedModifier;

    public override void Initialize()
    {
        base.Initialize();

        _buckleQuery = GetEntityQuery<BuckleComponent>();
        _surfaceQuery = GetEntityQuery<SurgerySurfaceComponent>();

        Subs.CVar(_config, ACCVars.SurgeryFailureThreshold, value => _failureThreshold = value, true);
        Subs.CVar(_config, ACCVars.SurgeryFailureChancePerSecond, value => _failureChancePerSecond = value, true);
        Subs.CVar(_config, ACCVars.SelfSurgeryFailurePenalty, value => _selfSurgeryPenalty = value, true);
        Subs.CVar(_config, ACCVars.SelfSurgeryFailureChance, value => _selfSurgeryFailureChance = value, true);
        Subs.CVar(_config, ACCVars.SurgeryFloorSpeedModifier, value => _floorSpeedModifier = value, true);
        Subs.CVar(_config, ACCVars.SurgeryAnesthesiaSpeedModifier, value => _anesthesiaSpeedModifier = value, true);
        Subs.CVar(_config, ACCVars.SurgeryAnalyzedSpeedModifier, value => _analyzedSpeedModifier = value, true);
    }

    [SubscribeLocalEvent]
    private void OnDeadValid(Entity<SurgeryDeadConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!_mobState.IsDead(args.Body))
            args.Cancelled = true;
    }

    public float GetSpeedModifier(EntityUid user, EntityUid patient)
    {
        return GetSurfaceModifier(patient) * GetDrunkModifier(user) * GetAnesthesiaModifier(patient) * GetAnalysisModifier(patient);
    }

    public bool IsSelfSurgeryPenalized(EntityUid user, EntityUid patient, EntityUid part, EntityUid step)
    {
        return user == patient
            && (!TryComp<SurgerySelfOperableStepComponent>(step, out var selfOperable)
                || selfOperable.RoboticOnly && !HasComp<RoboticBodyPartComponent>(part));
    }

    /// <summary>
    ///     How many times slower than ideal the step is; operating on yourself adds to it without taking more time.
    /// </summary>
    public float GetPenalty(float idealDuration, float duration, bool selfSurgery)
    {
        var penalty = duration / idealDuration;
        return selfSurgery ? penalty + _selfSurgeryPenalty : penalty;
    }

    public float GetCappedDuration(float idealDuration, float duration)
    {
        return MathF.Min(duration, idealDuration * _failureThreshold);
    }

    public float GetFailureChance(float idealDuration, float duration, bool selfSurgery = false)
    {
        var extraSeconds = duration - idealDuration * _failureThreshold;
        var chance = extraSeconds * _failureChancePerSecond;
        if (selfSurgery)
            chance = MathF.Max(chance, _selfSurgeryFailureChance);
        return Math.Clamp(chance, 0f, 0.99f);
    }

    /// <summary>
    ///     Rolls whether a finished step was botched and tells the surgeon how bad the conditions were.
    /// </summary>
    public bool RollStepFailure(EntityUid user, EntityUid part, float failureChance, float penalty)
    {
        if (failureChance <= 0f
            || !SharedRandomExtensions.PredictedProb(_timing, failureChance, GetNetEntity(user), GetNetEntity(part)))
            return false;

        var severity = (penalty - _failureThreshold) switch
        {
            < 0.5f => "almost",
            >= 2.5f => "impossible",
            >= 1.5f => "hard",
            _ => "other",
        };

        _popup.PopupPredicted(Loc.GetString("surgery-step-botched-self", ("severity", severity)),
            Loc.GetString("surgery-step-botched-others", ("user", Identity.Entity(user, EntityManager))),
            user,
            user,
            PopupType.MediumCaution);
        return true;
    }

    private float GetSurfaceModifier(EntityUid patient)
    {
        if (_buckleQuery.TryComp(patient, out var buckle) && buckle.BuckledTo is { } strap)
            return _surfaceQuery.CompOrNull(strap)?.SpeedModifier ?? _floorSpeedModifier;

        var xform = Transform(patient);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return _floorSpeedModifier;

        var tile = _map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
        var anchored = _map.GetAnchoredEntitiesEnumerator(xform.GridUid.Value, grid, tile);
        float? best = null;
        while (anchored.MoveNext(out var uid))
        {
            if (_surfaceQuery.TryComp(uid, out var surface))
                best = MathF.Max(best ?? surface.SpeedModifier, surface.SpeedModifier);
        }

        return best ?? _floorSpeedModifier;
    }

    /// <summary>
    ///     tgstation's drunkenness amount, read as the seconds of drunkenness left.
    /// </summary>
    public float GetDrunkenness(EntityUid uid)
    {
        if (!_status.TryGetMaxTime<DrunkStatusEffectComponent>(uid, out var drunk))
            return 0f;

        return drunk.EndEffectTime is { } end
            ? Math.Clamp((float) (end - _timing.CurTime).TotalSeconds, 0f, MaxDrunkSeconds)
            : MaxDrunkSeconds;
    }

    private float GetDrunkModifier(EntityUid user)
    {
        // tgstation's curve: a 12x slowdown at full drunkenness. Its Ballmer peak is left out, since our drunkenness works differently.
        return 1f / (1f + MathF.Pow(GetDrunkenness(user), 1.5f) / 90f);
    }

    private float GetAnesthesiaModifier(EntityUid patient)
    {
        return _status.HasEffectComp<ForcedSleepingStatusEffectComponent>(patient)
            || _status.HasEffectComp<AnalgesiaStatusEffectComponent>(patient)
            ? _anesthesiaSpeedModifier
            : 1f;
    }

    private float GetAnalysisModifier(EntityUid patient)
    {
        return HasComp<SurgicallyAnalyzedComponent>(patient) ? _analyzedSpeedModifier : 1f;
    }
}
