using Content.Shared.Inventory;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class ClothesWearingRequirements : ErpRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<TagPrototype>> Tags = new();

    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        var inventory = entityManager.System<InventorySystem>();
        var tags = entityManager.System<TagSystem>();

        if (!inventory.TryGetSlots(uid, out var slotDefinitions))
            return false;

        foreach (var slot in slotDefinitions)
        {
            if (!inventory.TryGetSlotEntity(uid, slot.Name, out var wornItem))
                continue;

            if (tags.HasAnyTag(wornItem.Value, Tags))
                return true;
        }
        return false;
    }
}
