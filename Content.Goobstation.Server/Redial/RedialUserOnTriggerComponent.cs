// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Server.Redial;

[RegisterComponent]
public sealed partial class RedialUserOnTriggerComponent : Component
{
    [DataField]
    public string Address = string.Empty;
}