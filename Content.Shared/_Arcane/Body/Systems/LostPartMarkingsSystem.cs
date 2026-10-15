// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Arcane.Body.Components;
using Content.Shared._Shitmed.Body.Part;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.Body.Systems;

public sealed class LostPartMarkingsSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        // The body drops the part's markings, so read them first while it still knows which are forced.
        SubscribeLocalEvent<HumanoidAppearanceComponent, BodyPartRemovedEvent>(OnBodyPartRemoved, before: [typeof(SharedBodySystem)]);
    }

    private void OnBodyPartRemoved(Entity<HumanoidAppearanceComponent> ent, ref BodyPartRemovedEvent args)
    {
        if (!_net.IsServer || TerminatingOrDeleted(ent))
            return;

        if (TryComp<LostPartMarkingsComponent>(ent, out var lost))
            lost.Markings.Remove(args.Slot);

        if (!TryComp<BodyPartAppearanceComponent>(args.Part, out var appearance))
            return;

        var markings = new Dictionary<HumanoidVisualLayers, List<Marking>>();
        foreach (var (layer, list) in appearance.Markings)
        {
            var category = MarkingCategoriesConversion.FromHumanoidVisualLayers(layer);
            foreach (var marking in list)
            {
                if (ent.Comp.MarkingSet.TryGetMarking(category, marking.MarkingId, out var bodyMarking))
                    markings.GetOrNew(layer).Add(new Marking(bodyMarking));
            }
        }

        if (markings.Count > 0)
            EnsureComp<LostPartMarkingsComponent>(ent).Markings[args.Slot] = markings;
    }

    public void RestoreMarkings(Entity<HumanoidAppearanceComponent?> body, Entity<BodyPartComponent> part)
    {
        if (!Resolve(body, ref body.Comp, false))
            return;

        // Losing a part hides all its sublayers, but attaching it shows only its own layer.
        if (part.Comp.ToHumanoidLayers() is { } partLayer)
        {
            var partLayers = GetPartLayers(body);
            var sublayers = HumanoidVisualLayersExtension.Sublayers(partLayer).Where(layer => !partLayers.Contains(layer));
            _humanoid.SetLayersVisibility(body, sublayers, true);
        }

        if (!TryComp<LostPartMarkingsComponent>(body, out var lost)
            || !_container.TryGetContainingContainer(part.Owner, out var slot)
            || !lost.Markings.Remove(slot.ID, out var markings)
            || !TryComp<BodyPartAppearanceComponent>(part, out var appearance))
            return;

        appearance.Markings = markings;
        Dirty(part, appearance);

        foreach (var (layer, list) in markings)
        {
            var category = MarkingCategoriesConversion.FromHumanoidVisualLayers(layer);
            foreach (var marking in list)
            {
                if (!body.Comp.MarkingSet.TryGetMarking(category, marking.MarkingId, out _))
                    _humanoid.AddMarking(body, marking.MarkingId, marking.MarkingColors, false, marking.Forced, body.Comp);
            }
        }

        Dirty(body.Owner, body.Comp);
    }

    private HashSet<HumanoidVisualLayers> GetPartLayers(EntityUid body)
    {
        var layers = new HashSet<HumanoidVisualLayers>();
        if (!TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Prototype is not { } bodyProto)
            return layers;

        foreach (var slot in _proto.Index(bodyProto).Slots.Values)
        {
            if (slot.Part is { } partProto
                && _proto.Index(partProto).TryGetComponent<BodyPartComponent>(out var part, _componentFactory)
                && part.ToHumanoidLayers() is { } layer)
                layers.Add(layer);
        }

        return layers;
    }
}
