// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Surgery;

/// <summary>
///     A step that shocks the brain with a defibrillator, reviving the patient even after the body has rotted.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryRevivalStepComponent : Component
{
    [DataField]
    public FixedPoint2 SuccessBrainDamage = 15;

    [DataField]
    public FixedPoint2 SuccessIntegrityFloor = 20;

    [DataField]
    public FixedPoint2 FailureBrainDamage = 50;

    [DataField]
    public FixedPoint2 FailureIntegrityFloor = 1;
}
