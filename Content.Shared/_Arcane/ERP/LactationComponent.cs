using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ERP.Components;

[RegisterComponent]
public sealed partial class LactationComponent : Component
{
    [DataField]
    public float NutrientCostPerUnit = 2f;

    [DataField]
    public float MinHunger = 50f;
}
