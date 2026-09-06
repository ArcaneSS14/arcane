using Content.Server._Arcane.InvisibilityHeart.Components;
using Content.Server.Body.Systems;
using Content.Server.Ghost.Roles.Events;
using Content.Shared._Arcane.Invisibility;
using Content.Shared.Body.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Arcane.Invisibility;

/// <summary>
///     Fires once when an arcane invisibility ghost role finally spawns in. The body is fully
///     assembled by this point, so we can swap out the ordinary heart for the arcane one that
///     grants <see cref="ArcaneInvisibilityComponent"/> via its <c>onAdd</c>, then play the
///     materialization effect.
/// </summary>
public sealed class ArcaneArrivalSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly EntProtoId AppearEffect = "EffectSlasherJauntOut";

    private static readonly SoundSpecifier AppearSound =
        new SoundPathSpecifier(new ResPath("/Audio/_Goobstation/Effects/Slasher/SlasherJauntAppear.ogg"));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostRoleSpawnerUsedEvent>(OnSpawnerUsed);
    }

    private void OnSpawnerUsed(GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<InvisibilityHeartComponent>(args.Spawned, out var marker))
            return;

        ImplantHeart(args.Spawned, marker.OrganProto, marker.OrganSlot);

        // Defense in depth: even if the implant somehow missed the slot, an expanding
        // arrival should still be invisible. Cutting the heart out removes the component.
        EnsureComp<ArcaneInvisibilityComponent>(args.Spawned);

        Spawn(AppearEffect, _transform.GetMapCoordinates(args.Spawned));
        _audio.PlayPvs(AppearSound, args.Spawned);
    }

    private void ImplantHeart(EntityUid bodyId, EntProtoId organProto, string slotId)
    {
        foreach (var (partUid, partComp) in _body.GetBodyChildren(bodyId))
        {
            if (!partComp.Organs.ContainsKey(slotId))
                continue;

            foreach (var (existingUid, organComp) in _body.GetPartOrgans(partUid, partComp))
            {
                if (organComp.SlotId != slotId)
                    continue;

                _body.RemoveOrgan(existingUid);
                QueueDel(existingUid);
                break;
            }

            var organ = Spawn(organProto);
            _body.TryCreateOrganSlot(partUid, slotId, out _, partComp);
            if (!_body.InsertOrgan(partUid, organ, slotId, partComp))
            {
                QueueDel(organ);
                return;
            }

            return;
        }
    }
}