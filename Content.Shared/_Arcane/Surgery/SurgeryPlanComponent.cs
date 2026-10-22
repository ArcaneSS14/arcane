// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

/// <summary>
/// The surgery a surgeon is working towards, preferred when a tool click could advance several surgeries.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryPlanComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Part;

    [DataField, AutoNetworkedField]
    public EntProtoId? Surgery;

    public override bool SendOnlyToOwner => true;
}
