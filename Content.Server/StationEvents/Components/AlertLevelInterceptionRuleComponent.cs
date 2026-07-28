// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.StationEvents.Events;

namespace Content.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(AlertLevelInterceptionRule))]
public sealed partial class AlertLevelInterceptionRuleComponent : Component
{
    /// <summary>
    /// Alert level to set the station to when the event starts.
    /// </summary>
    [DataField]
    public string AlertLevel = "blue";

    /// <summary>
    /// Goobstation.
    /// Whether or not to override the current alert level, if it isn't green.
    /// </summary>
    [DataField]
    public bool OverrideAlert;

    /// <summary>
    /// Goobstation.
    /// Whether the alert level should be changeable.
    /// </summary>
    [DataField]
    public bool Locked;
}