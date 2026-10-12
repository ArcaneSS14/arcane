// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;

namespace Content.Shared._Arcane.Body.Components;

/// <summary>
/// Markings of body parts this body lost, by part slot container, kept for a part regrown into that slot.
/// </summary>
[RegisterComponent]
public sealed partial class LostPartMarkingsComponent : Component
{
    [ViewVariables]
    public Dictionary<string, Dictionary<HumanoidVisualLayers, List<Marking>>> Markings = new();
}
