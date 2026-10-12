// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Arcane.Body.Components;
using Content.Shared._Shitmed.Body.Part;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Body.Systems;

/// <summary>
/// Gives a regrown body part the markings of the part it replaces.
/// </summary>
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

        SubscribeLocalEvent<HumanoidAppearanceComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);
    }

    // The body drops a lost part's markings, so keep them for the part regrown into that slot.
    private void OnBodyPartRemoved(Entity<HumanoidAppearanceComponent> ent, ref BodyPartRemovedEvent args)
    {
        if (!_net.IsServer || TerminatingOrDeleted(ent))
            return;

        if (TryComp<LostPartMarkingsComponent>(ent, out var lost))
            lost.Markings.Remove(args.Slot);

        if (!TryComp<BodyPartAppearanceComponent>(args.Part, out var appearance)
            || appearance.Markings.Count == 0)
            return;

        EnsureComp<LostPartMarkingsComponent>(ent).Markings[args.Slot] = appearance.Markings.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Select(m => new Marking(m)).ToList());
    }

    /// <summary>
    /// Shows a part regrown into a slot with the markings of the part lost from that slot.
    /// Call after attaching it, so the part appearance first picks up its base layer from the body.
    /// </summary>
    public void RestoreMarkings(Entity<HumanoidAppearanceComponent?> body, Entity<BodyPartComponent> part)
    {
        if (!Resolve(body, ref body.Comp, false))
            return;

        // Losing a part hides all of its sublayers, but attaching it only shows its own layer,
        // so markings it never recorded (e.g. hair) would otherwise stay hidden.
        // Sublayers drawn by other parts (hands, feet) are shown when those parts are attached.
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

        foreach (var list in markings.Values)
        {
            foreach (var marking in list)
                _humanoid.AddMarking(body, marking.MarkingId, marking.MarkingColors, false, true, body.Comp);
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
