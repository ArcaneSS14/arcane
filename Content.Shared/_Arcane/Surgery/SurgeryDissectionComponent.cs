using Content.Shared.Damage;

namespace Content.Shared._Arcane.Surgery;

[RegisterComponent]
public sealed partial class SurgeryDissectionComponent : Component
{
    [DataField(required: true)]
    public List<DissectionReward> Rewards = new();

    [DataField(required: true)]
    public float BotchMultiplier;

    [DataField(required: true)]
    public DamageSpecifier Damage = new();
}

public sealed partial class DissectionReward : SurgeryBodyKind
{
    [DataField(required: true)]
    public int Points;
}
