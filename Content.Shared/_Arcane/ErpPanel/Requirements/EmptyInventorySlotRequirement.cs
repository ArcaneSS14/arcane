using Content.Shared.Inventory;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class EmptyInventorySlotRequirement : ErpRequirement
{
    [DataField(required: true)]
    public string Slot = string.Empty;

    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        var inventory = entityManager.System<InventorySystem>();

        if (!inventory.HasSlot(uid, Slot))
            return false;

        return !inventory.TryGetSlotEntity(uid, Slot, out _);
    }
}
