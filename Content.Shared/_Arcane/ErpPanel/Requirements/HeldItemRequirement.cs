using Content.Shared.Hands.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class HeldItemRequirement : InvertableErpRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<TagPrototype>> Tags = new();

    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        var hands = entityManager.System<SharedHandsSystem>();
        var tags = entityManager.System<TagSystem>();

        var hasHeldItem = false;

        foreach (var held in hands.EnumerateHeld(uid))
        {
            if (tags.HasAnyTag(held, Tags))
            {
                hasHeldItem = true;
                break;
            }
        }

        return Inverted ? !hasHeldItem : hasHeldItem;
    }
}
