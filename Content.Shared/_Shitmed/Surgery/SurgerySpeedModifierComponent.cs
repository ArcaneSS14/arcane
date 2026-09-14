// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgerySpeedModifierComponent : Component
{
    [DataField]
    public float SpeedModifier = 2f; // Arcane-Edit: 1.5 -> 2
}
