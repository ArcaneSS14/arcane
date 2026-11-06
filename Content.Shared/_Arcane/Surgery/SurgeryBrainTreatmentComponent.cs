using Robust.Shared.Prototypes;
using Content.Shared.Traits;

namespace Content.Shared._Arcane.Surgery;

[RegisterComponent]
public sealed partial class SurgeryBrainTreatmentComponent : Component
{
    [DataField]
    public float HealFraction = 0.25f;

    [DataField]
    public float FailureDamageFraction = 0.3f;

    [DataField]
    public ProtoId<TraitPrototype> FailureTrait = "Narcolepsy";
}
