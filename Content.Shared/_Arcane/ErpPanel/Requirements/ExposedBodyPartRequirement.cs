using Content.Shared.Body.Part;
using Content.Shared.Inventory;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class ExposedBodyPartRequirement : ErpRequirement
{
    [DataField(required: true)]
    public BodyPartType Part;

    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        var coveringSlots = Part switch
        {
            BodyPartType.Chest => SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING | SlotFlags.UNDERSHIRT,
            BodyPartType.Groin => SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING | SlotFlags.UNDERWEAR,
            _ => SlotFlags.NONE,
        };

        if (coveringSlots == SlotFlags.NONE)
            return false;

        var inventory = entityManager.System<InventorySystem>();
        if (!inventory.TryGetContainerSlotEnumerator(uid, out var slots, coveringSlots))
            return false;

        return !slots.NextItem(out _);
    }
}
