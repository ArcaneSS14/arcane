// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Graphics;
using Content.Shared._Arcane.Clothing.Components;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Arcane.Clothing.Systems;

/// <summary>
/// Composites a wearer offscreen so clothing cutout layers can erase underlying body pixels.
/// </summary>
public sealed class ClothingSpriteCutoutSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> CompositeShader = "ArcaneClothingComposite";

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, EntityUid> _wearers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingSpriteCutoutComponent, EquipmentVisualsUpdatedEvent>(OnVisualsUpdated);
        SubscribeLocalEvent<ClothingSpriteCutoutComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<ClothingSpriteCutoutComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        foreach (var (item, wearer) in _wearers)
        {
            _sprite.RemovePostShader((wearer, CompOrNull<SpriteComponent>(wearer)), GetShaderId(item));
        }
        _wearers.Clear();

        base.Shutdown();
    }

    private void OnVisualsUpdated(Entity<ClothingSpriteCutoutComponent> ent, ref EquipmentVisualsUpdatedEvent args)
    {
        if (!TryComp<ClothingComponent>(ent, out var clothing)
            || clothing.InSlotFlag is not { } slotFlags
            || (clothing.Slots & slotFlags) == SlotFlags.NONE
            || args.RevealedLayers.Count == 0)
        {
            RemoveComposite(ent);
            return;
        }

        SetComposite(ent, args.Equipee);
    }

    private void OnUnequipped(Entity<ClothingSpriteCutoutComponent> ent, ref GotUnequippedEvent args)
    {
        RemoveComposite(ent);
    }

    private void OnShutdown(Entity<ClothingSpriteCutoutComponent> ent, ref ComponentShutdown args)
    {
        RemoveComposite(ent);
    }

    private void SetComposite(EntityUid item, EntityUid wearer)
    {
        RemoveComposite(item);

        if (!TryComp<SpriteComponent>(wearer, out var sprite))
            return;

        // Force the wearer into an isolated target so the cutout clears body pixels, not the world.
        _sprite.SetPostShader((wearer, sprite), new SpriteComponent.PostShaderArgs(
            GetShaderId(item), _prototype.Index(CompositeShader).Instance())
        {
            Before = ContentPostShaderIds.AfterBaseEffects,
        });
        _wearers[item] = wearer;
    }

    private void RemoveComposite(EntityUid item)
    {
        if (!_wearers.Remove(item, out var wearer))
            return;

        _sprite.RemovePostShader((wearer, CompOrNull<SpriteComponent>(wearer)), GetShaderId(item));
    }

    private static string GetShaderId(EntityUid item) => $"arcane-clothing-cutout-{item}";
}
