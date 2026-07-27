// SPDX-License-Identifier: MIT

namespace Content.Server.Research.Components;

[RegisterComponent]
public sealed partial class ResearchPointSourceComponent : Component
{
    // Orion-Start
    [DataField]
    public string PointType = "General";
    // Orion-End

    [DataField("pointspersecond"), ViewVariables(VVAccess.ReadWrite)]
    public int PointsPerSecond;

    // Orion-Start
    [DataField]
    public string? RequiredInfrastructure;
    // Orion-End

    [DataField("active"), ViewVariables(VVAccess.ReadWrite)]
    public bool Active;
}
