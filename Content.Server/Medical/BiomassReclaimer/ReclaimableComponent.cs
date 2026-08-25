// SPDX-License-Identifier: MIT
 
namespace Content.Server.Medical.BiomassReclaimer;
 
/// <summary>
/// Marks an entity as processable by a biomass reclaimer,
/// independent of MobStateComponent or ProduceComponent.
/// Used for things like raw meat.
/// </summary>
[RegisterComponent]
public sealed partial class BiomassReclaimableComponent : Component
{
    /// <summary>
    /// Multiplier applied to biomass yield for this entity, analogous to
    /// BiomassReclaimerComponent.ProduceYieldMultiplier for plants.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float YieldMultiplier = 1f;
}
