using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Arcane.ERP;

public enum ArousalPhase : byte
{
    Calm,
    Interested,
    Aroused,
    Heated,
    Peak,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true), AutoGenerateComponentPause]
public sealed partial class ArousalComponent : Component
{
    /// <summary>
    /// Authoritative arousal value as of LastChangeTime.
    /// Use Shared ArousalSystem.GetArousal to get the current value accounting for decay.
    /// </summary>
    [AutoNetworkedField]
    public float LastValue;

    /// <summary>
    /// Time at which LastValue was last set.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan LastChangeTime;

    /// <summary>
    /// Passive decay per second.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DecayRate = 0.3f;

    [DataField]
    public float MaxArousal = 100f;

    /// <summary>
    /// Registered passive arousal sources. Key is source ID, value is gain per second.
    /// Use Shared ArousalSystem.SetPassiveSource and SharedArousalSystem.RemovePassiveSource.
    /// </summary>
    public Dictionary<string, float> PassiveSources = [];

    /// <summary>
    /// Total passive gain per second — sum of all <see cref="PassiveSources"/>.
    /// </summary>
    public float PassiveGainRate
    {
        get
        {
            var total = 0f;
            foreach (var rate in PassiveSources.Values)
                total += rate;
            return total;
        }
    }

    /// <summary>
    /// When to next check for phase transitions from passive decay.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextPhaseCheckAt;

    [AutoNetworkedField]
    public ArousalPhase CurrentPhase;

    /// <summary>
    /// When the last orgasm occurred.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan LastOrgasmAt;

    /// <summary>
    /// Arousal gain is blocked until this time (refractory period after orgasm).
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan RefractoryUntil;

    /// <summary>
    /// How long the refractory period lasts after orgasm.
    /// </summary>
    [DataField]
    public TimeSpan RefractoryDuration = TimeSpan.FromSeconds(30);

    public ArousalPhase ComputePhase(float arousal) => arousal switch
    {
        < 20f => ArousalPhase.Calm,
        < 40f => ArousalPhase.Interested,
        < 70f => ArousalPhase.Aroused,
        < 100f => ArousalPhase.Heated,
        _ => ArousalPhase.Peak,
    };
}
