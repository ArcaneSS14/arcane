// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

/// <summary>
/// Surgery steps that always count as done on this body part and are hidden from the surgery UI.
/// </summary>
[RegisterComponent]
public sealed partial class SurgerySkipStepsComponent : Component
{
    [DataField(required: true)]
    public HashSet<EntProtoId> Steps = new();
}
