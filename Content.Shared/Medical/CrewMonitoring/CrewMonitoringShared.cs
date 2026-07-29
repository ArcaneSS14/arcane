// SPDX-License-Identifier: MIT

using Content.Shared.Medical.SuitSensor;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.CrewMonitoring;

[Serializable, NetSerializable]
public enum CrewMonitoringUIKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class CrewMonitoringState : BoundUserInterfaceState
{
    public List<SuitSensorStatus> Sensors;
    public bool IsEmagged; // Orion

    public CrewMonitoringState(List<SuitSensorStatus> sensors, bool isEmagged) // Orion-Edit: Added emag
    {
        Sensors = sensors;
        IsEmagged = isEmagged; // Orion
    }
}
