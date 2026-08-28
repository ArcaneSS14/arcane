// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Arcane.Slime;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Arcane.Slime;

/// <summary>
/// Handles the actual limb regrowth for slime people, eating hunger and thirst in the process.
/// </summary>
public sealed partial class SlimeRegrowSystem : SharedSlimeRegrowSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ThirstSystem _thirst = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlimeRegrowComponent, SlimeRegrowLimbEvent>(OnSlimeRegrowLimb);
    }

    private void OnSlimeRegrowLimb(Entity<SlimeRegrowComponent> ent, ref SlimeRegrowLimbEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;

        if (!TryComp<BodyComponent>(user, out var body)
            || body.Prototype is null
            || !_body.TryGetRootPart(user, out _, body))
            return;

        var candidates = FindMissingLimbs(user, body);

        if (candidates.Count == 0)
        {
            _popup.PopupClient(Loc.GetString(ent.Comp.NoLimbPopup), user, user);
            args.Handled = true;
            return;
        }

        if (!TryComp<HungerComponent>(user, out var hunger)
            || _hunger.GetHunger(hunger) < ent.Comp.HungerCost)
        {
            _popup.PopupClient(Loc.GetString(ent.Comp.TooHungryPopup), user, user);
            args.Handled = true;
            return;
        }

        if (!TryComp<ThirstComponent>(user, out var thirst)
            || thirst.CurrentThirst < ent.Comp.ThirstCost)
        {
            _popup.PopupClient(Loc.GetString(ent.Comp.TooThirstyPopup), user, user);
            args.Handled = true;
            return;
        }

        var candidate = _random.Pick(candidates);

        _hunger.ModifyHunger(user, -ent.Comp.HungerCost, hunger);
        _thirst.ModifyThirst(user, thirst, -ent.Comp.ThirstCost);

        if (!TryGrowLimb(candidate.ParentId, candidate.SlotId, candidate.Slot))
        {
            _popup.PopupClient(Loc.GetString(ent.Comp.NoLimbPopup), user, user);
            args.Handled = true;
            return;
        }

        _audio.PlayPredicted(ent.Comp.Sound, user, user);
        _popup.PopupClient(Loc.GetString(ent.Comp.RegrowPopup), user, user);

        args.Handled = true;
    }

    /// <summary>
    /// Traverses the body prototype starting from the root, collecting every missing,
    /// non-vital part slot that could be regrown.
    /// </summary>
    private List<MissingLimb> FindMissingLimbs(EntityUid uid, BodyComponent body)
    {
        var missing = new List<MissingLimb>();

        if (body.Prototype is not { } protoId
            || !_body.TryGetRootPart(uid, out var rootPart, body))
            return missing;

        var prototype = _proto.Index(protoId);

        var frontier = new Queue<string>();
        frontier.Enqueue(prototype.Root);

        // Child -> Parent connection.
        var cameFrom = new Dictionary<string, string>();
        cameFrom[prototype.Root] = prototype.Root;

        // Maps slot to its relevant entity.
        var cameFromEntities = new Dictionary<string, EntityUid>();
        cameFromEntities[prototype.Root] = rootPart.Value.Owner;

        while (frontier.TryDequeue(out var currentSlotId))
        {
            var currentSlot = prototype.Slots[currentSlotId];

            foreach (var connection in currentSlot.Connections)
            {
                if (!cameFrom.TryAdd(connection, currentSlotId))
                    continue;

                var connectionSlot = prototype.Slots[connection];
                var parentEntity = cameFromEntities[currentSlotId];

                if (_container.TryGetContainer(parentEntity, SharedBodySystem.GetPartSlotContainerId(connection), out var container)
                    && container.ContainedEntities.Count > 0)
                {
                    cameFromEntities[connection] = container.ContainedEntities[0];
                    frontier.Enqueue(connection);
                    continue;
                }

                if (connectionSlot.Part is not { } partId
                    || !_proto.TryIndex<EntityPrototype>(partId, out var partProto)
                    || !partProto.TryGetComponent<BodyPartComponent>(out var partComp, _componentFactory)
                    || (partComp.PartType & BodyPartType.Vital) != 0)
                    continue;

                missing.Add(new MissingLimb(parentEntity, connection, connectionSlot));
            }
        }

        return missing;
    }

    private bool TryGrowLimb(EntityUid parentId, string slotId, BodyPrototypeSlot slot)
    {
        if (slot.Part is not { } partId)
            return false;

        var childPart = Spawn(partId, new EntityCoordinates(parentId, Vector2.Zero));
        var childPartComp = Comp<BodyPartComponent>(childPart);

        if (!_body.TryCreatePartSlotAndAttach(parentId, slotId, childPart, childPartComp.PartType, childPartComp.Symmetry))
        {
            Log.Error($"Failed to regrow part {partId} into slot {slotId} of {ToPrettyString(parentId)}");
            QueueDel(childPart);
            return false;
        }

        foreach (var (organSlotId, organProtoId) in slot.Organs)
        {
            _body.TryCreateOrganSlot(childPart, organSlotId, out _);
            SpawnInContainerOrDrop(organProtoId, childPart, SharedBodySystem.GetOrganContainerId(organSlotId));
        }

        return true;
    }

    private readonly record struct MissingLimb(EntityUid ParentId, string SlotId, BodyPrototypeSlot Slot);
}