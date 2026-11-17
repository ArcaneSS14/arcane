// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BloodBiowareComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? Source;

    [DataField, AutoNetworkedField]
    public float BleedMultiplier = 1f;
}
