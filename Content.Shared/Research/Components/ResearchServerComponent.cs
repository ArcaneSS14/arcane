// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Orion.Research;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Research.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ResearchServerComponent : Component
{
    /// <summary>
    /// The name of the server
    /// </summary>
    [AutoNetworkedField]
    [DataField("serverName"), ViewVariables(VVAccess.ReadWrite)]
    public string ServerName = "RND-Server"; // Orion-Edit: Fix name

    /// <summary>
    /// The amount of points on the server.
    /// </summary>
    [AutoNetworkedField]
    [DataField("points"), ViewVariables(VVAccess.ReadWrite)]
    public int Points;

    // Orion-Start
    /// <summary>
    /// Multi-point balance for research network economy.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public List<ResearchPointAmount> PointBalances = new()
    {
        new()
        {
            Type = "General",
            Amount = 0,
        }
    };

    /// <summary>
    /// Network key used for grouping related RnD servers.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public string NetworkId = "ResearchNet";

    /// <summary>
    /// Snapshot of network log entries for connected clients.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public List<ResearchLogEntry> Logs = new();
    // Orion-End

    /// <summary>
    /// A unique numeric id representing the server
    /// </summary>
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public int Id;

    /// <summary>
    /// Entities connected to the server
    /// </summary>
    /// <remarks>
    /// This is not safe to read clientside
    /// </remarks>
    [ViewVariables(VVAccess.ReadOnly)]
    public List<EntityUid> Clients = new();

    [DataField("nextUpdateTime", customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextUpdateTime = TimeSpan.Zero;

    [DataField("researchConsoleUpdateTime"), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan ResearchConsoleUpdateTime = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Event raised on a server's clients when the point value of the server is changed.
/// </summary>
/// <param name="Server"></param>
/// <param name="Total"></param>
/// <param name="Delta"></param>
[ByRefEvent]
public readonly record struct ResearchServerPointsChangedEvent(EntityUid Server, int Total, int Delta);

// Orion-Start
[ByRefEvent]
public readonly record struct ResearchServerPointTypeChangedEvent(EntityUid Server, string Type, int Total, int Delta);
// Orion-End

/* // Orion-Edit: Use ResearchServerGetPointsPerSecondByTypeEvent
/// <summary>
/// Event raised every second to calculate the amount of points added to the server.
/// </summary>
/// <param name="Server"></param>
/// <param name="Points"></param>
[ByRefEvent]
public record struct ResearchServerGetPointsPerSecondEvent(EntityUid Server, int Points);
*/

// Orion-Start
/// <summary>
/// Event raised every second to calculate the amount of points added to the server.
/// </summary>
/// <param name="Server"></param>
/// <param name="Points"></param>
[ByRefEvent]
public record struct ResearchServerGetPointsPerSecondByTypeEvent(EntityUid Server, List<ResearchPointAmount> Points);
// Orion-End
