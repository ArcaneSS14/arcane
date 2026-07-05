using Content.Shared._Arcane.ERP.Organs;
using Content.Shared._Shitmed.Humanoid.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ERP;

public sealed class EroticOrganSpawnSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Run after SharedBodySystem so body parts are already spawned when we look for them.
        SubscribeLocalEvent<EroticOrgansComponent, MapInitEvent>(OnMapInit, after: [typeof(SharedBodySystem)]);
        SubscribeLocalEvent<EroticOrgansComponent, ProfileLoadFinishedEvent>(OnProfileLoaded);
        SubscribeLocalEvent<EroticOrgansComponent, SexChangedEvent>(OnSexChanged);
    }

    private void OnMapInit(Entity<EroticOrgansComponent> ent, ref MapInitEvent args)
    {
        if (!_net.IsServer)
            return;

        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        SpawnEroticOrgans(ent, ent.Comp, humanoid.Sex);
    }

    private void OnProfileLoaded(Entity<EroticOrgansComponent> ent, ref ProfileLoadFinishedEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        humanoid.HideLayersOnEquip.Add(HumanoidVisualLayers.ErpGroin); // Arcane-edit
        humanoid.HideLayersOnEquip.Add(HumanoidVisualLayers.ErpChest); // Arcane-edit

        if (!_net.IsServer)
            return;

        RemoveEroticOrgans(ent);
        SpawnEroticOrgans(ent, ent.Comp, humanoid.Sex);
    }

    private void OnSexChanged(Entity<EroticOrgansComponent> ent, ref SexChangedEvent args)
    {
        if (!_net.IsServer)
            return;

        RemoveEroticOrgans(ent);
        SpawnEroticOrgans(ent, ent.Comp, args.NewSex);
    }

    private void SpawnEroticOrgans(EntityUid uid, EroticOrgansComponent def, Sex sex)
    {
        var groin = GetBodyPartOfType(uid, BodyPartType.Groin);
        var chest = GetBodyPartOfType(uid, BodyPartType.Chest);

        if (groin.HasValue)
        {
            TrySpawnOrgans(uid, groin.Value, def.GroinCommon);

            if (sex is Sex.Male or Sex.Futanari)
                TrySpawnOrgans(uid, groin.Value, def.GroinMale);

            if (sex is Sex.Female or Sex.Futanari)
                TrySpawnOrgans(uid, groin.Value, def.GroinFemale);
        }

        if (chest.HasValue && sex is Sex.Female or Sex.Futanari)
            TrySpawnOrgans(uid, chest.Value, def.ChestFemale);

        var ev = new EroticOrgansSpawnedEvent();
        RaiseLocalEvent(uid, ref ev);
    }

    private void RemoveEroticOrgans(EntityUid bodyUid)
    {
        var organs = _body.GetBodyOrganEntityComps<EroticOrganComponent>((bodyUid, null));
        foreach (var organ in organs)
        {
            _body.RemoveOrgan(organ.Owner, organ.Comp2);
            QueueDel(organ.Owner);
        }
    }

    private void TrySpawnOrgans(EntityUid bodyUid, EntityUid partUid, List<EroticOrganEntry> organs)
    {
        foreach (var entry in organs)
            TrySpawnOrgan(bodyUid, partUid, entry.Proto, entry.Slot);
    }

    private void TrySpawnOrgan(EntityUid bodyUid, EntityUid partUid, EntProtoId protoId, string slotId)
    {
        if (!_proto.HasIndex(protoId))
            return;

        _body.TryCreateOrganSlot(partUid, slotId, out _);

        var containerId = SharedBodySystem.GetOrganContainerId(slotId);
        if (_containers.TryGetContainer(partUid, containerId, out var container)
            && container.ContainedEntities.Count > 0)
            return;

        var organEnt = Spawn(protoId, Transform(partUid).Coordinates);
        if (!_body.InsertOrgan(partUid, organEnt, slotId))
            QueueDel(organEnt);
    }

    private EntityUid? GetBodyPartOfType(EntityUid bodyUid, BodyPartType partType)
    {
        foreach (var (partUid, _) in _body.GetBodyChildrenOfType(bodyUid, partType))
            return partUid;

        return null;
    }
}
