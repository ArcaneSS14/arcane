// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;

namespace Content.Shared._Arcane.Body.Components;

[RegisterComponent]
public sealed partial class LostPartMarkingsComponent : Component
{
    [ViewVariables]
    public Dictionary<string, Dictionary<HumanoidVisualLayers, List<Marking>>> Markings = new();
}
