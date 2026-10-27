using Content.Goobstation.Common.Interactions;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Common.MartialArts;
using Content.Shared._Arcane.ConflictWear.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ConflictWear;

public sealed partial class ConflictWearSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConflictWearComponent, BeingEquippedAttemptEvent>(OnBeingEquippedAttempt);
        SubscribeLocalEvent<ConflictWearComponent, UseInHandAttemptEvent>(OnUseInHandAttempt);
    }

    private void OnBeingEquippedAttempt(Entity<ConflictWearComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<ClothingComponent>(ent, out var clothing))
            return;

        if (CheckConflicts(ent.Owner, ent.Comp, args.EquipTarget, clothing.Slots, out var popup))
        {
            args.Cancel();
            args.Reason = popup;
        }
    }

    private void OnUseInHandAttempt(Entity<ConflictWearComponent> ent, ref UseInHandAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_net.IsServer)
            return;

        if (CheckConflicts(ent.Owner, ent.Comp, args.User, SlotFlags.NONE, out var popup))
        {
            args.Cancel();
            _popup.PopupEntity(popup, args.User, args.User, PopupType.MediumCaution);
        }
    }

    private bool CheckConflicts(EntityUid item, ConflictWearComponent comp, EntityUid user, SlotFlags slots,
        out string popup)
    {
        popup = string.Empty;

        foreach (var ruleId in comp.Rules)
        {
            if (!_proto.TryIndex(ruleId, out var rule))
                continue;

            if (rule.Slots.Count > 0 && slots != SlotFlags.NONE)
            {
                var slotAllowed = false;
                foreach (var slot in rule.Slots)
                {
                    if (!_inventory.TryGetSlot(user, slot, out var slotDef))
                        continue;

                    if ((slots & slotDef.SlotFlags) != 0)
                    {
                        slotAllowed = true;
                        break;
                    }
                }

                if (!slotAllowed)
                    continue;
            }

            if (rule.ConflictingPrototypes.Count > 0)
            {
                foreach (var protoId in rule.ConflictingPrototypes)
                {
                    if (ProtoIdMatches(item, protoId))
                        continue;

                    if (HasEquippedPrototype(user, protoId))
                    {
                        var failPopup = new LocId(rule.FailPopup?.Id ?? "conflict-wear-fail");
                        popup = Loc.GetString(failPopup,
                            ("item", item),
                            ("conflict", protoId));
                        return true;
                    }
                }
            }

            if (CheckKnowledgeConflict(item, user, rule, out popup))
                return true;
        }

        return false;
    }
    private bool CheckKnowledgeConflict(EntityUid item, EntityUid user, ConflictWearRulePrototype rule,
        out string popup)
    {
        popup = string.Empty;

        if (rule.ConflictingKnowledge.Count == 0)
            return false;

        if (!TryComp<MartialArtsKnowledgeComponent>(user, out var martial))
            return false;

        var form = martial.MartialArtsForm.ToString();
        if (!rule.ConflictingKnowledge.Contains(form))
            return false;

        var failPopup = new LocId(rule.FailPopup?.Id ?? "conflict-wear-fail");
        var conflictName = Loc.TryGetString($"conflict-wear-knowledge-{form}", out var loc)
            ? loc
            : form;

        popup = Loc.GetString(failPopup,
            ("item", item),
            ("conflict", conflictName));
        return true;
    }

    public bool CheckKnowledgeConflicts(EntityUid user, string knowledge, out string popup)
    {
        popup = string.Empty;

        var query = EntityQueryEnumerator<ConflictWearComponent, ClothingComponent>();
        while (query.MoveNext(out var item, out var comp, out _))
        {
            foreach (var ruleId in comp.Rules)
            {
                if (!_proto.TryIndex(ruleId, out var rule))
                    continue;

                if (!rule.Bidirectional)
                    continue;

                if (!rule.ConflictingKnowledge.Contains(knowledge))
                    continue;

                if (!IsEquipped(user, item))
                    continue;

                var failPopup = new LocId(rule.FailPopup?.Id ?? "conflict-wear-fail");
                popup = Loc.GetString(failPopup,
                    ("item", item),
                    ("conflict", knowledge));
                return true;
            }
        }

        return false;
    }

    private bool HasEquippedPrototype(EntityUid user, EntProtoId protoId)
    {
        var query = EntityQueryEnumerator<ClothingComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!IsEquipped(user, uid))
                continue;

            if (ProtoIdMatches(uid, protoId))
                return true;
        }

        return false;
    }

    private bool IsEquipped(EntityUid wearer, EntityUid item)
    {
        if (!TryComp<InventoryComponent>(wearer, out var inv))
            return false;

        foreach (var slot in inv.Slots)
        {
            if (!_inventory.TryGetSlotEntity(wearer, slot.Name, out var slotEntity, inv))
                continue;

            if (slotEntity == item)
                return true;
        }

        return false;
    }

    private bool ProtoIdMatches(EntityUid uid, EntProtoId protoId)
    {
        var meta = MetaData(uid);
        return meta.EntityPrototype?.ID == protoId.Id;
    }
}
