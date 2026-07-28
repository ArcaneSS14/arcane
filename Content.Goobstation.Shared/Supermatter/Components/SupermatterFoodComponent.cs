// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Shared.Supermatter.Components;

[RegisterComponent]
public sealed partial class SupermatterFoodComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("energy")]
    public int Energy { get; set; } = 1;
}