using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class TagBlacklistRequirement : InvertableErpRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<TagPrototype>> Tags = new();

    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        var tagSystem = entityManager.System<TagSystem>();
        var hasAnyTag = tagSystem.HasAnyTag(uid, Tags);

        return Inverted ? hasAnyTag : !hasAnyTag;
    }
}
