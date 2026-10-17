using System.Linq;
using Content.Client.Clothing;
using Content.Shared._Arcane.Clothing.Components;
using Content.Shared.Clothing;
using Content.Shared.Item;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Client._Arcane.Clothing.Systems;

public sealed class ArcaneItemClothingVisualsSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArcaneItemClothingVisualsComponent, GetEquipmentVisualsEvent>(
            OnGetEquipmentVisuals,
            after: new[] { typeof(ClientClothingSystem) });
        SubscribeLocalEvent<ArcaneItemClothingVisualsComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<ArcaneItemClothingVisualsComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
    }

    private void OnContainerChanged(Entity<ArcaneItemClothingVisualsComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (ent.Comp.ContainerWhitelist != null && !ent.Comp.ContainerWhitelist.Contains(args.Container.ID))
            return;

        _item.VisualsChanged(ent);
    }

    private void OnContainerChanged(Entity<ArcaneItemClothingVisualsComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (ent.Comp.ContainerWhitelist != null && !ent.Comp.ContainerWhitelist.Contains(args.Container.ID))
            return;

        _item.VisualsChanged(ent);
    }

    private void OnGetEquipmentVisuals(
        Entity<ArcaneItemClothingVisualsComponent> ent,
        ref GetEquipmentVisualsEvent args)
    {
        if (ent.Comp.ClothingVisuals.Count == 0)
            return;

        if (!ent.Comp.ClothingVisuals.TryGetValue(args.Slot, out var layers) || layers.Count == 0)
            return;

        var contained = _container.GetAllContainers(ent.Owner)
            .Where(c => ent.Comp.ContainerWhitelist?.Contains(c.ID) ?? true)
            .SelectMany(c => c.ContainedEntities)
            .ToArray();

        if (contained.Length == 0)
            return;

        var i = 0;
        foreach (var entry in layers)
        {
            var matched = false;
            foreach (var containedEnt in contained)
            {
                if (!_whitelist.IsWhitelistPassOrNull(entry.Whitelist, containedEnt))
                    continue;

                matched = true;
                break;
            }

            if (!matched)
                continue;

            var layer = entry.Layer;

            var key = layer.MapKeys?.FirstOrDefault();
            if (key == null)
            {
                key = $"{args.Slot}-arcane-{i}";
                i++;
            }

            args.Layers.Add((key, layer));
        }
    }
}
