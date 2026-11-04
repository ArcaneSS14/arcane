// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

/// <summary>
///     Wedges the held pill into the patient's teeth, or takes one out.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryDentalImplantStepComponent : Component
{
    [DataField]
    public bool Remove;

    [DataField]
    public EntProtoId Action = "ActionActivateDentalPill";
}
