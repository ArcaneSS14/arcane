using System.Linq;
using Content.Client.Eye;
using Content.Client.Humanoid;
using Content.Client.Inventory;
using Content.Goobstation.Common.Clothing;
using Content.Shared.Clothing;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Arcane.DirectionalLayering;

/// <summary>
///     Reorders a humanoid's hair, tail, cloak and mask layers by which side of the character faces the camera.
///     Every pass pulls these blocks out of the sprite and re-inserts them next to base layers that never move, so the
///     result depends only on the current view, never on the views applied before it.
/// </summary>
public sealed class DirectionalLayeringSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private static readonly object[] LegLayers =
    {
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RFoot,
        HumanoidVisualLayers.LFoot,
    };

    private static readonly object[] BaseLayers = Enum.GetValues<HumanoidVisualLayers>().Cast<object>().ToArray();

    private readonly Dictionary<EntityUid, LayeringState> _states = new();
    private readonly Dictionary<(string MarkingId, string RsiState), string> _markingKeys = new();

    private sealed class LayeringState
    {
        public DirectionalView? View;
        public DirectionalView? DummyView;
        public bool NativeCaptured;
        public object? HairAnchor;
        public object? TailAnchor;
        public bool TailBelowNeck;
    }

    private enum DirectionalView
    {
        Front,
        Side,
        Back,
    }

    public override void Initialize()
    {
        base.Initialize();

        // The view is read from the final world and eye rotation of this frame, right before the sprites render.
        UpdatesAfter.Add(typeof(TransformSystem));
        UpdatesAfter.Add(typeof(EyeLerpingSystem));

        SubscribeLocalEvent<HumanoidAppearanceComponent, HumanoidAppearanceUpdatedEvent>(OnAppearanceUpdated);
        SubscribeLocalEvent<EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ComponentRemove>(OnRemove);
    }

    public override void Shutdown()
    {
        _states.Clear();
        _markingKeys.Clear();
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        // Polled every frame instead of reacting to MoveEvent: parent and grid rotation, eye rotation and server
        // corrections of a predicted rotation all change the rendered direction without a reliable event.
        var eyeRotation = _eyeManager.CurrentEye.Rotation;
        var query = EntityQueryEnumerator<HumanoidAppearanceComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var humanoid, out var sprite, out var xform))
        {
            var state = GetState(uid);
            var view = state.DummyView
                ?? (sprite.EnableDirectionOverride
                    ? GetView(sprite.DirectionOverride)
                    : GetView(_transform.GetWorldRotation(xform) + eyeRotation));

            if (state.View == view)
                continue;

            ApplyOrdering((uid, humanoid, sprite), state, view);
        }
    }

    /// <summary>
    ///     Orders a preview dummy for the direction its SpriteView is rendered with.
    /// </summary>
    public void ApplyDummyOrdering(EntityUid uid, Direction direction)
    {
        if (!TryComp(uid, out HumanoidAppearanceComponent? humanoid) ||
            !TryComp(uid, out SpriteComponent? sprite))
        {
            return;
        }

        var state = GetState(uid);
        state.DummyView = GetView(direction);
        ApplyOrdering((uid, humanoid, sprite), state, state.DummyView.Value);
    }

    private void OnAppearanceUpdated(EntityUid uid, HumanoidAppearanceComponent component, HumanoidAppearanceUpdatedEvent args)
    {
        if (TryComp(uid, out SpriteComponent? sprite))
            Reapply((uid, component, sprite));
    }

    private void OnEquipmentVisualsUpdated(EquipmentVisualsUpdatedEvent args)
    {
        if (args.Slot != "neck" && args.Slot != "back" && args.Slot != "mask" ||
            !TryComp(args.Equipee, out HumanoidAppearanceComponent? humanoid) ||
            !TryComp(args.Equipee, out SpriteComponent? sprite))
        {
            return;
        }

        Reapply((args.Equipee, humanoid, sprite));
    }

    private void OnRemove(EntityUid uid, HumanoidAppearanceComponent component, ComponentRemove args)
    {
        _states.Remove(uid);
    }

    private LayeringState GetState(EntityUid uid)
    {
        if (!_states.TryGetValue(uid, out var state))
        {
            state = new LayeringState();
            _states[uid] = state;
        }

        return state;
    }

    // Layers were rebuilt by another system; restore the last applied view, or let the next frame pick one.
    private void Reapply(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        var state = GetState(ent.Owner);
        if (state.View is { } view)
            ApplyOrdering(ent, state, view);
    }

    private void ApplyOrdering(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, LayeringState state, DirectionalView view)
    {
        state.View = view;

        var hair = GetHairKeys(ent);
        var tail = GetTailKeys(ent, hair);

        // Only this system moves these blocks, so the first pass still sees the species' native order.
        if (!state.NativeCaptured)
        {
            state.NativeCaptured = true;
            state.HairAnchor = FindAnchorAbove(ent, hair, hair, tail);
            state.TailAnchor = FindAnchorAbove(ent, tail, hair, tail);
            state.TailBelowNeck = TryGetBlockStart(ent, tail, out var tailStart) &&
                                  TryGetIndex(ent, "neck", out var neckIdx) &&
                                  tailStart < neckIdx;
        }

        var cloakAnchor = GetCloakAnchor(ent);
        var cloak = cloakAnchor != null ? GetSlotKeys(ent, "neck", hair, tail) : new List<object>();
        var mask = IsMaskOnMainLayer(ent) ? GetSlotKeys(ent, "mask", hair, tail) : new List<object>();

        var blocks = ExtractBlocks(ent, hair, tail, cloak, mask);
        var hairLayers = blocks[0];
        var tailLayers = blocks[1];
        var cloakLayers = blocks[2];
        var maskLayers = blocks[3];

        if (cloakLayers.Count > 0 && TryGetIndex(ent, cloakAnchor!, out var cloakAnchorIdx))
            InsertBlock(ent, cloakLayers, cloakAnchorIdx + 1);

        if (hairLayers.Count > 0)
        {
            InsertBlock(ent, hairLayers, TryGetClusterTop(ent, out var clusterTop)
                ? clusterTop + 1
                : GetNativeIndex(ent, state.HairAnchor));
        }

        var hairTop = hairLayers.Count > 0 ? hairLayers[^1].Key : null;

        if (maskLayers.Count > 0)
        {
            var maskAnchor = hairTop ?? "mask";
            if (TryGetIndex(ent, maskAnchor, out var maskAnchorIdx))
                InsertBlock(ent, maskLayers, maskAnchorIdx + 1);
        }

        if (tailLayers.Count > 0)
            InsertBlock(ent, tailLayers, GetTailIndex(ent, state, view, hairTop));
    }

    private int? GetTailIndex(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        LayeringState state,
        DirectionalView view,
        object? hairTop)
    {
        switch (view)
        {
            // Facing the camera the tail points away from it, so it goes behind every layer.
            case DirectionalView.Front:
                return 0;
            case DirectionalView.Side when TryGetLowestLeg(ent, out var legIdx):
                return legIdx;
            // A tail drawn under the clothes (resomi) stays there; any other tail covers the hoisted hair.
            case DirectionalView.Back when !state.TailBelowNeck &&
                                           hairTop != null &&
                                           TryGetIndex(ent, hairTop, out var hairTopIdx):
                return hairTopIdx + 1;
            default:
                return GetNativeIndex(ent, state.TailAnchor);
        }
    }

    // A null anchor means nothing stable sat above the block, so it goes back on top.
    private int? GetNativeIndex(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, object? anchor)
    {
        return anchor != null && TryGetIndex(ent, anchor, out var index) ? index : null;
    }

    private bool TryGetIndex(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, object key, out int index)
    {
        index = 0;
        return key switch
        {
            Enum enumKey => _sprite.LayerMapTryGet((ent.Owner, ent.Comp2), enumKey, out index, false),
            string stringKey => _sprite.LayerMapTryGet((ent.Owner, ent.Comp2), stringKey, out index, false),
            _ => false,
        };
    }

    private void SetIndex(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, object key, int index)
    {
        switch (key)
        {
            case Enum enumKey:
                _sprite.LayerMapSet((ent.Owner, ent.Comp2), enumKey, index);
                break;
            case string stringKey:
                _sprite.LayerMapSet((ent.Owner, ent.Comp2), stringKey, index);
                break;
        }
    }

    private bool TryGetBlockStart(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, List<object> keys, out int start)
    {
        start = int.MaxValue;
        foreach (var key in keys)
        {
            if (TryGetIndex(ent, key, out var index))
                start = Math.Min(start, index);
        }

        return start != int.MaxValue;
    }

    private List<object> GetHairKeys(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        var keys = new List<object> { HumanoidVisualLayers.Hair };
        AddMarkingLayerKeys(ent, HumanoidVisualLayers.Hair, keys);
        return FilterExisting(ent, keys, null, null);
    }

    // Tail markings layered onto TailBehind or TailBehindBackpack (moth wings) split themselves per view already.
    private List<object> GetTailKeys(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, List<object> hair)
    {
        var keys = new List<object> { HumanoidVisualLayers.Tail, HumanoidVisualLayers.Wings };
        AddMarkingLayerKeys(ent, HumanoidVisualLayers.Tail, keys);
        AddMarkingLayerKeys(ent, HumanoidVisualLayers.Wings, keys);
        return FilterExisting(ent, keys, hair, null);
    }

    private List<object> GetSlotKeys(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        string slot,
        List<object> hair,
        List<object> tail)
    {
        var keys = new List<object>();
        if (TryComp(ent.Owner, out InventorySlotsComponent? slots) &&
            slots.VisualLayerKeys.TryGetValue(slot, out var slotKeys))
        {
            keys.AddRange(slotKeys);
        }

        return FilterExisting(ent, keys, hair, tail);
    }

    private List<object> FilterExisting(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        List<object> keys,
        List<object>? taken,
        List<object>? takenToo)
    {
        var result = new List<object>(keys.Count);
        foreach (var key in keys)
        {
            if (!result.Contains(key) &&
                taken?.Contains(key) != true &&
                takenToo?.Contains(key) != true &&
                TryGetIndex(ent, key, out _))
            {
                result.Add(key);
            }
        }

        return result;
    }

    /// <summary>
    ///     Adds the keys of every marking layer drawn on <paramref name="layer"/>, resolving per-state layering the same
    ///     way the appearance system does, so membership follows where a layer is drawn rather than its category.
    /// </summary>
    private void AddMarkingLayerKeys(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        HumanoidVisualLayers layer,
        List<object> keys)
    {
        foreach (var markingList in ent.Comp1.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (!_markingManager.TryGetMarking(marking, out var prototype))
                    continue;

                foreach (var sprite in prototype.Sprites)
                {
                    if (sprite is not SpriteSpecifier.Rsi rsi)
                        continue;

                    var spriteLayer = prototype.BodyPart;
                    if (prototype.Layering != null &&
                        prototype.Layering.TryGetValue(rsi.RsiState, out var layerName) &&
                        !Enum.TryParse(layerName, out spriteLayer))
                    {
                        continue;
                    }

                    if (spriteLayer == layer)
                        keys.Add(GetMarkingKey(marking.MarkingId, rsi.RsiState));
                }
            }
        }
    }

    private string GetMarkingKey(string markingId, string rsiState)
    {
        if (_markingKeys.TryGetValue((markingId, rsiState), out var key))
            return key;

        key = $"{markingId}-{rsiState}";
        _markingKeys[(markingId, rsiState)] = key;
        return key;
    }

    // The nearest base or slot layer above the block in the native order; blocks go back directly below it.
    private object? FindAnchorAbove(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        List<object> block,
        List<object> hair,
        List<object> tail)
    {
        var end = int.MinValue;
        foreach (var key in block)
        {
            if (TryGetIndex(ent, key, out var index))
                end = Math.Max(end, index);
        }

        if (end == int.MinValue)
            return null;

        object? best = null;
        var bestIdx = int.MaxValue;

        void Consider(object candidate)
        {
            if (hair.Contains(candidate) ||
                tail.Contains(candidate) ||
                !TryGetIndex(ent, candidate, out var index) ||
                index <= end ||
                index >= bestIdx)
            {
                return;
            }

            best = candidate;
            bestIdx = index;
        }

        foreach (var candidate in BaseLayers)
        {
            Consider(candidate);
        }

        if (_inventory.TryGetSlots(ent.Owner, out var slots))
        {
            foreach (var slot in slots)
            {
                Consider(slot.Name);
            }
        }

        return best;
    }

    // Above the "neck" slot, or above the worn bag for species that draw the back slot over the neck slot.
    private object? GetCloakAnchor(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        if (!TryGetIndex(ent, "neck", out var anchorIdx))
            return null;

        object anchor = "neck";
        if (TryComp(ent.Owner, out InventorySlotsComponent? slots) &&
            slots.VisualLayerKeys.TryGetValue("back", out var backKeys))
        {
            foreach (var key in backKeys)
            {
                if (!TryGetIndex(ent, key, out var index) || index <= anchorIdx)
                    continue;

                anchor = key;
                anchorIdx = index;
            }
        }

        return anchor;
    }

    // A pulled-down mask renders on its alternative slot below the hair and never moves.
    private bool IsMaskOnMainLayer(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        if (!_inventory.TryGetSlotEntity(ent.Owner, "mask", out var mask))
            return false;

        var mapLayerEv = new GetActualMapLayerEvent("mask");
        RaiseLocalEvent(mask.Value, ref mapLayerEv);
        return (mapLayerEv.MapLayer == "mask" || !TryGetIndex(ent, mapLayerEv.MapLayer, out _)) &&
               TryGetIndex(ent, "mask", out _);
    }

    /// <summary>
    ///     The top of the head-trim cluster: the "mask", "HeadSide" and "HeadTop" base layers and their marking layers.
    ///     The hair is hoisted above it in every view so it covers the ears, unless the character keeps the ears above
    ///     the hair.
    /// </summary>
    private bool TryGetClusterTop(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, out int top)
    {
        var keys = new List<object> { "mask", HumanoidVisualLayers.HeadSide, HumanoidVisualLayers.HeadTop };
        if (!ent.Comp1.EarsAboveHair)
        {
            AddMarkingLayerKeys(ent, HumanoidVisualLayers.HeadSide, keys);
            AddMarkingLayerKeys(ent, HumanoidVisualLayers.HeadTop, keys);
        }

        top = int.MinValue;
        foreach (var key in keys)
        {
            if (TryGetIndex(ent, key, out var index))
                top = Math.Max(top, index);
        }

        return top != int.MinValue;
    }

    private bool TryGetLowestLeg(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, out int lowest)
    {
        lowest = int.MaxValue;
        foreach (var leg in LegLayers)
        {
            if (TryGetIndex(ent, leg, out var index))
                lowest = Math.Min(lowest, index);
        }

        return lowest != int.MaxValue;
    }

    /// <summary>
    ///     Removes the layers of every block from the sprite and returns them per block in draw order. Keys that share
    ///     one layer stay together, so none of them is lost from the layer map.
    /// </summary>
    private List<(object Key, Layer Layer)>[] ExtractBlocks(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        params List<object>[] blocks)
    {
        var entries = new List<(int Block, object Key, int Index)>();
        for (var i = 0; i < blocks.Length; i++)
        {
            foreach (var key in blocks[i])
            {
                if (TryGetIndex(ent, key, out var index))
                    entries.Add((i, key, index));
            }
        }

        // Top-most first, so every removal leaves the indices still to remove untouched.
        entries.Sort((a, b) => b.Index.CompareTo(a.Index));

        var result = new List<(object Key, Layer Layer)>[blocks.Length];
        for (var i = 0; i < blocks.Length; i++)
        {
            result[i] = new List<(object Key, Layer Layer)>();
        }

        Layer? previous = null;
        var previousIndex = -1;
        var previousBlock = -1;
        foreach (var (block, key, index) in entries)
        {
            if (index != previousIndex)
            {
                _sprite.RemoveLayer((ent.Owner, ent.Comp2), index, out previous, false);
                previousIndex = index;
                previousBlock = block;
            }

            if (previous != null)
                result[previousBlock].Add((key, previous));
        }

        foreach (var block in result)
        {
            block.Reverse();
        }

        return result;
    }

    // A null target appends the block on top of the sprite.
    private void InsertBlock(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        List<(object Key, Layer Layer)> block,
        int? target)
    {
        Layer? previous = null;
        var index = -1;
        foreach (var (key, layer) in block)
        {
            if (layer != previous)
            {
                index = _sprite.AddLayer((ent.Owner, ent.Comp2), layer, target);
                if (target != null)
                    target = index + 1;

                previous = layer;
            }

            SetIndex(ent, key, index);
        }
    }

    /// <summary>
    ///     Mirrors <see cref="Layer.GetDirection"/> for 4-directional layers, including the anti-flicker bias, so the
    ///     ordering matches the sprite states that actually render.
    /// </summary>
    private static DirectionalView GetView(Angle angle)
    {
        var ang = angle.Reduced().FlipPositive().Theta;
        var mod = (Math.Floor(ang / MathHelper.PiOver2) % 2) - 0.5;
        var modTheta = ang + mod * DirectionBias;
        var quadrant = (int) Math.Round(modTheta / MathHelper.PiOver2) % 4;

        return quadrant switch
        {
            0 => DirectionalView.Front,
            2 => DirectionalView.Back,
            _ => DirectionalView.Side,
        };
    }

    private static DirectionalView GetView(Direction direction)
    {
        return direction switch
        {
            Direction.South => DirectionalView.Front,
            Direction.North => DirectionalView.Back,
            _ => DirectionalView.Side,
        };
    }
}
