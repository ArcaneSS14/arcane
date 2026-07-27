// SPDX-License-Identifier: MIT

using Content.Server.Anomaly.Effects;

namespace Content.Server.Anomaly.Components;

/// <summary>
/// Shuffle Particle types in some situations
/// </summary>
[RegisterComponent, Access(typeof(ShuffleParticlesAnomalySystem))]
public sealed partial class ShuffleParticlesAnomalyComponent : Component
{
    /// <summary>
    /// Prob() chance to randomize particle types after Anomaly pulation
    /// </summary>
    [DataField]
    public bool ShuffleOnPulse;

    /// <summary>
    /// Prob() chance to randomize particle types after APE or CHIMP projectile
    /// </summary>
    [DataField]
    public bool ShuffleOnParticleHit;

    /// <summary>
    /// Chance to random particles
    /// </summary>
    [DataField]
    public float Prob = 0.5f;
}