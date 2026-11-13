// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared.Kitchen.Components;

[RegisterComponent]
public sealed partial class RecipeSubstituteComponent : Component
{
    [DataField]
    public List<EntProtoId> Substitutes = new();
}
