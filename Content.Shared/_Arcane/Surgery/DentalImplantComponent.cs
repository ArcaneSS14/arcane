// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Arcane.Surgery;

/// <summary>
///     A pill wedged into the teeth, burst with an action.
/// </summary>
[RegisterComponent]
public sealed partial class DentalImplantComponent : Component
{
    [DataField]
    public EntityUid? Action;

    [DataField]
    public TimeSpan CriticalDelay = TimeSpan.FromSeconds(2.5);
}
