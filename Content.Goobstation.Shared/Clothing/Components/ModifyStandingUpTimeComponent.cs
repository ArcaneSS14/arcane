// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Shared.Clothing.Components;

[RegisterComponent]
public sealed partial class ModifyStandingUpTimeComponent : Component
{
    [DataField]
    public float Multiplier = 1f;
}