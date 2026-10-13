// SPDX-FileCopyrightText: 2025 Doctor-Cpu <77215380+Doctor-Cpu@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GabyChangelog <agentepanela2@gmail.com>
// SPDX-FileCopyrightText: 2025 Will-Oliver-Br <164823659+Will-Oliver-Br@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Inventory;
using Content.Shared._Arcane.WashingMachine;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Arcane.WashingMachine;

public sealed partial class WashingMachineSystem : SharedWashingMachineSystem
{
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly HumanoidVisualLayers[] HiddenAppearanceLayers =
    [
        HumanoidVisualLayers.Special,
        HumanoidVisualLayers.Hair,
        HumanoidVisualLayers.FacialHair,
        HumanoidVisualLayers.Face,
        HumanoidVisualLayers.Head,
        HumanoidVisualLayers.Snout,
        HumanoidVisualLayers.SnoutCover,
        HumanoidVisualLayers.HeadSide,
        HumanoidVisualLayers.HeadTop,
        HumanoidVisualLayers.Eyes,
        HumanoidVisualLayers.RArm,
        HumanoidVisualLayers.LArm,
        HumanoidVisualLayers.RHand,
        HumanoidVisualLayers.LHand,
        HumanoidVisualLayers.Handcuffs,
        HumanoidVisualLayers.Tracheas,
    ];

    private static readonly string[] HiddenClothingSlots =
        ["head", "eyes", "ears", "earsright", "mask", "neck"];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WashingMachineStuckComponent, ComponentAdd>(OnStuckAdd);
    }

    private void OnStuckAdd(EntityUid uid, WashingMachineStuckComponent component, ComponentAdd args)
    {
        ApplyHiddenLayers(uid, component, true);
    }

    private List<object> GetHiddenLayerKeys(EntityUid uid, InventorySlotsComponent? inventory)
    {
        var keys = new List<object>(HiddenAppearanceLayers.Length);

        foreach (var visual in HiddenAppearanceLayers)
            keys.Add(visual);

        if (inventory != null)
        {
            foreach (var slot in HiddenClothingSlots)
            {
                if (inventory.VisualLayerKeys.TryGetValue(slot, out var revealed))
                    keys.AddRange(revealed);
            }
        }

        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            AddMarkingLayerKeys(humanoid, keys);

        return keys;
    }

    private void AddMarkingLayerKeys(HumanoidAppearanceComponent humanoid, List<object> keys)
    {
        foreach (var markingList in humanoid.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (!_markingManager.TryGetMarking(marking, out var markerProto) ||
                    !HiddenAppearanceLayers.Contains(markerProto.BodyPart))
                    continue;

                foreach (var spriteSpec in markerProto.Sprites)
                {
                    if (spriteSpec is SpriteSpecifier.Rsi rsi)
                        keys.Add($"{marking.MarkingId}-{rsi.RsiState}");
                }
            }
        }
    }

    private void ApplyHiddenLayers(EntityUid uid, WashingMachineStuckComponent component, bool snapshot)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        TryComp<InventorySlotsComponent>(uid, out var inventory);

        var keys = GetHiddenLayerKeys(uid, inventory);
        foreach (var key in keys)
        {
            if (!TryGetLayerIndex((uid, sprite), key, out var index))
                continue;

            if (snapshot && !component.PreLayerVisibility.ContainsKey(key))
                component.PreLayerVisibility[key] = sprite[index].Visible;

            _sprite.LayerSetVisible((uid, sprite), index, false);
        }
    }

    private bool TryGetLayerIndex(Entity<SpriteComponent?> sprite, object key, out int index)
    {
        if (key is Enum enumKey)
            return _sprite.LayerMapTryGet(sprite, enumKey, out index, false);

        if (key is string strKey)
            return _sprite.LayerMapTryGet(sprite, strKey, out index, false);

        index = 0;
        return false;
    }

    protected override void RestoreStuckVisuals(Entity<WashingMachineStuckComponent> ent)
    {
        base.RestoreStuckVisuals(ent);

        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        TryComp<InventorySlotsComponent>(ent.Owner, out var inventory);

        var keys = GetHiddenLayerKeys(ent.Owner, inventory);
        foreach (var key in keys)
        {
            if (!TryGetLayerIndex((ent.Owner, sprite), key, out var index))
                continue;

            var visible = ent.Comp.PreLayerVisibility.GetValueOrDefault(key, true);
            _sprite.LayerSetVisible((ent.Owner, sprite), index, visible);
        }

        ent.Comp.PreLayerVisibility.Clear();
    }
}