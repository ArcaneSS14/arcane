// SPDX-License-Identifier: MIT

using Content.Shared.Medical.SuitSensor;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Server.Medical.CrewMonitoring;

[RegisterComponent]
[Access(typeof(CrewMonitoringConsoleSystem))]
public sealed partial class CrewMonitoringConsoleComponent : Component
{
    /// <summary>
    ///     List of all currently connected sensors to this console.
    /// </summary>
    public Dictionary<string, SuitSensorStatus> ConnectedSensors = new();

    /// <summary>
    ///     After what time sensor consider to be lost.
    /// </summary>
    [DataField("sensorTimeout"), ViewVariables(VVAccess.ReadWrite)]
    public float SensorTimeout = 10f;

    // Orion-Start
    /// <summary>
    /// What departments this monitor can see. If empty, shows all departments.
    /// YAML example: departments: [ Medical, Security ]
    /// </summary>
    [DataField]
    public List<ProtoId<DepartmentPrototype>> Departments = new();

    /// <summary>
    ///     Enable or disable alerts.
    /// </summary>
    [DataField]
    public bool DoAlert;

    /// <summary>
    ///     Time interval between alerts in seconds.
    /// </summary>
    [DataField]
    public float AlertTime = 30f;

    /// <summary>
    ///     Sound to play when alert is triggered.
    /// </summary>
    [DataField]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/_Orion/Machines/crew_monitoring_alert.ogg");

    [DataField]
    public AudioParams AlertAudioParams = AudioParams.Default.WithVolume(-8f).WithVariation(0.25f);

    /// <summary>
    ///     Timestamp of the last played alert.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextAlertTime = TimeSpan.Zero;

    [DataField(serverOnly: true)]
    public Color? NormalLightColor { get; set; }

    [DataField(serverOnly: true)]
    public float? NormalLightEnergy { get; set; }

    [DataField(serverOnly: true)]
    public float? NormalLightRadius { get; set; }

    /// <summary>
    ///     Permanently displays everyone regardless of sensor mode.
    ///     Set via YAML for special consoles (e.g. death squad monitor).
    /// </summary>
    [DataField]
    public bool IsEmagged = false;

    /// <summary>
    ///     Expiry time for a temporary emag triggered by the emag tool.
    ///     Null if not temporarily emagged. Never set by YAML.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan? EmagExpireTime = null;

    public static readonly TimeSpan EmagDuration = TimeSpan.FromSeconds(15);

    /// <summary>
    ///     Emag sound effects.
    /// </summary>
    [DataField]
    public SoundSpecifier SparkSound = new SoundCollectionSpecifier("sparks")
    {
        Params = AudioParams.Default.WithVolume(8),
    };
    // Orion-End
}
