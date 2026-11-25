using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class TagWhitelistRequirement : InvertableErpRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<TagPrototype>> Tags = new();

    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        var tagSystem = entityManager.System<TagSystem>();
        var hasAllTags = true;

        foreach (var tag in Tags)
        {
            if (!tagSystem.HasTag(uid, tag))
            {
                hasAllTags = false;
                break;
            }
        }

        return Inverted ? !hasAllTags : hasAllTags;
    }
}
