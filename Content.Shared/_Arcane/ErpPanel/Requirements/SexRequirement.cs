using Content.Shared.Humanoid;
using Content.Shared.Tag;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class SexRequirement : InvertableErpRequirement
{
    [DataField(required: true)]
    public HashSet<Sex> Sexes = new();

    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        var hasSex = false;

        if (entityManager.TryGetComponent<HumanoidAppearanceComponent>(uid, out var humanoid))
        {
            hasSex = Sexes.Contains(humanoid.Sex);
        }
        else
        {
            var tagSystem = entityManager.System<TagSystem>();

            foreach (var sex in Sexes)
            {
                var tag = sex switch
                {
                    Sex.Male => "MobGenderMale",
                    Sex.Female => "MobGenderFemale",
                    Sex.Futanari => "MobGenderFutanari",
                    _ => null
                };

                if (tag != null && tagSystem.HasTag(uid, tag))
                {
                    hasSex = true;
                    break;
                }
            }
        }

        return Inverted ? !hasSex : hasSex;
    }
}
