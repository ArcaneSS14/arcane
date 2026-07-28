// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Orion.Research;
using Content.Shared.Random;
using Content.Shared.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Research.TechnologyDisk.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class TechnologyDiskComponent : Component
{
    /// <summary>
    /// The recipe that will be added. If null, one will be randomly generated
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public List<ProtoId<LatheRecipePrototype>>? Recipes;

    /// <summary>
    /// A weighted random prototype for how rare each tier should be.
    /// </summary>
    [DataField]
    public ProtoId<WeightedRandomPrototype> TierWeightPrototype = "TechDiskTierWeights";

    // Orion-Start
    /// <summary>
    /// Optional technology snapshot that can be imported into another network.
    /// </summary>
    [DataField]
    public List<ProtoId<TechnologyPrototype>>? StoredTechnologies;

    /// <summary>
    /// Optional point snapshot for network transfer workflows.
    /// </summary>
    [DataField]
    public List<ResearchPointAmount>? StoredPointBalances;
    // Orion-End
}
