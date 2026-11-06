// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NerveBiowareComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? Source;

    [DataField, AutoNetworkedField]
    public float StunMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float StaminaMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public bool ShockImmune;

    [DataField]
    public float GroundingSiemensCoefficient = 0.25f;
}
