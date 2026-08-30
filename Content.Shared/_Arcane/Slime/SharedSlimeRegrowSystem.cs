using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Arcane.Slime;

/// <summary>
/// Handles granting and removing the slime limb regrow action, and the action itself.
/// The server is authoritative: it alone finds the missing limb, picks one, grows it and
/// spends the hunger/thirst, and drives the popup/audio feedback. The client performs no
/// predicted feedback, so it can't show a success that the server would reject.
/// </summary>
public abstract partial class SharedSlimeRegrowSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ThirstSystem _thirst = default!;
    [Dependency] private readonly TraumaSystem _trauma = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlimeRegrowComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SlimeRegrowComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SlimeRegrowComponent, SlimeRegrowLimbEvent>(OnSlimeRegrowLimb);
    }

    private void OnMapInit(Entity<SlimeRegrowComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.ActionEnt = _actions.AddAction(ent, ent.Comp.ActionId);
        Dirty(ent, ent.Comp);
    }

    private void OnShutdown(Entity<SlimeRegrowComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEnt);
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

        if (!_net.IsServer)
            return;

        var candidates = FindMissingLimbs(user, body);

        if (candidates.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.NoLimbPopup), user, user);
            return;
        }

        if (!TryComp<HungerComponent>(user, out var hunger)
            || _hunger.GetHunger(hunger) < ent.Comp.HungerCost)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.TooHungryPopup), user, user);
            return;
        }

        if (!TryComp<ThirstComponent>(user, out var thirst)
            || thirst.CurrentThirst < ent.Comp.ThirstCost)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.TooThirstyPopup), user, user);
            return;
        }

        var candidate = _random.Pick(candidates);

        if (!TryGrowLimb(candidate.ParentId, candidate.SlotId, candidate.Slot))
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.NoLimbPopup), user, user);
            return;
        }

        // Resources are only spent once the limb actually regrew.
        _hunger.ModifyHunger(user, -ent.Comp.HungerCost, hunger);
        _thirst.ModifyThirst(user, thirst, -ent.Comp.ThirstCost);

        _popup.PopupEntity(Loc.GetString(ent.Comp.RegrowPopup), user, user);
        _audio.PlayEntity(ent.Comp.Sound, user, user);

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

        // Slots already traversed.
        var visited = new HashSet<string> { prototype.Root };

        // Maps slot to its relevant entity.
        var slotEntities = new Dictionary<string, EntityUid>();
        slotEntities[prototype.Root] = rootPart.Value.Owner;

        while (frontier.TryDequeue(out var currentSlotId))
        {
            var currentSlot = prototype.Slots[currentSlotId];

            foreach (var connection in currentSlot.Connections)
            {
                if (!visited.Add(connection))
                    continue;

                var connectionSlot = prototype.Slots[connection];
                var parentEntity = slotEntities[currentSlotId];

                if (_container.TryGetContainer(parentEntity, SharedBodySystem.GetPartSlotContainerId(connection), out var container)
                    && container.ContainedEntities.Count > 0)
                {
                    slotEntities[connection] = container.ContainedEntities[0];
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

        // Regrowing a limb also heals the stump (Dismemberment trauma) its removal left behind,
        // otherwise it would still need surgery to clean up before the socket is usable again.
        // Only clear the trauma matching the regrown part so unrelated dismemberments stay intact.
        if (_trauma.TryGetWoundableTrauma(parentId, out var stumpTraumas, TraumaSystem.Dismemberment))
        {
            foreach (var trauma in stumpTraumas)
            {
                if (trauma.Comp.TargetType is not { } targetType
                    || targetType != (childPartComp.PartType, childPartComp.Symmetry))
                    continue;

                _trauma.RemoveTrauma(trauma);
            }
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
