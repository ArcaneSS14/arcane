// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Arcane.Body.Components;

/// <summary>
/// Excludes this head, chest, or groin from vital damage, so it affects health like a limb.
/// </summary>
[RegisterComponent]
public sealed partial class NonVitalBodyPartComponent : Component { }
