using Content.Shared._Arcane.ERP;
using Content.Shared.Inventory;
using Robust.Shared.Serialization;
using Content.Shared._Arcane.Body.Part;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class ExposedBodyPartRequirement : InvertableErpRequirement
{
    [DataField(required: true)]
    public ErpBodyPartType Part;

    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        var coveringSlots = Part switch
        {
            ErpBodyPartType.Chest => SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING | SlotFlags.UNDERSHIRT,
            ErpBodyPartType.Groin => SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING | SlotFlags.UNDERWEAR,
            ErpBodyPartType.Butt => SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING | SlotFlags.UNDERWEAR | SlotFlags.PLUG,

            _ => SlotFlags.NONE,
        };

        if (coveringSlots == SlotFlags.NONE)
            return false;

        var inventory = entityManager.System<InventorySystem>();
        if (!inventory.TryGetContainerSlotEnumerator(uid, out var slots, coveringSlots))
            return false;

        var isExposed = true;

        while (slots.MoveNext(out var containerSlot))
        {
            if (containerSlot?.ContainedEntity is not { } item)
                continue;

            if (entityManager.TryGetComponent<CondomComponent>(item, out var condom) && !condom.Full)
                continue;

            isExposed = false;
            break;
        }

        return Inverted ? !isExposed : isExposed;
    }
}
