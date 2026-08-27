// SPDX-License-Identifier: MIT

using Content.Server.Botany.Systems;
using Content.Shared._Orion.Construction.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Botany.Components;

[RegisterComponent]
[Access(typeof(SeedExtractorSystem))]
public sealed partial class SeedExtractorComponent : Component
{
    /// <summary>
    /// The minimum amount of seed packets dropped.
    /// </summary>
    [DataField("baseMinSeeds"), ViewVariables(VVAccess.ReadWrite)]
    public int BaseMinSeeds = 1;

    /// <summary>
    /// The maximum amount of seed packets dropped.
    /// </summary>
    [DataField("baseMaxSeeds"), ViewVariables(VVAccess.ReadWrite)]
    public int BaseMaxSeeds = 3;

    // Orion-Start
    [DataField]
    public ProtoId<MachinePartPrototype> ServoPart = "Servo";

    [ViewVariables(VVAccess.ReadWrite)]
    public float SeedMultiplier = 1f;
    // Orion-End
}
