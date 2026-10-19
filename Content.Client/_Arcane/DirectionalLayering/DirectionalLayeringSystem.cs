using System.Linq;
using Content.Client.Eye;
using Content.Client.Humanoid;
using Content.Client.Inventory;
using Content.Shared.Clothing;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Arcane.DirectionalLayering;

/// <summary>
///     Keeps a humanoid's ears against the hair and the cloak over the bag, and moves the tail by which side of the
///     character faces the camera.
///     Every pass pulls these blocks out of the sprite and re-inserts them next to base layers that never move, so the
///     result depends only on the current view, never on the views applied before it.
/// </summary>
public sealed class DirectionalLayeringSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly object[] LegLayers =
    {
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RFoot,
        HumanoidVisualLayers.LFoot,
    };

    private readonly Dictionary<EntityUid, LayeringState> _states = new();
    private readonly Dictionary<(string MarkingId, string RsiState), string> _markingKeys = new();

    private sealed class LayeringState
    {
        public DirectionalView? View;
        public DirectionalView? DummyView;
        public bool NativeCaptured;
        public ISpriteLayer? TailAnchor;
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
            // Nullspace dummies are only drawn by SpriteViews, which render them with an unrotated eye.
            var eye = xform.MapID == MapId.Nullspace ? Angle.Zero : eyeRotation;
            var view = state.DummyView
                ?? (sprite.EnableDirectionOverride
                    ? GetView(sprite.DirectionOverride)
                    : GetView(_transform.GetWorldRotation(xform) + eye));

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

    /// <summary>
    ///     Copies a humanoid's sprite laid out for the direction the copy is drawn with, instead of the view the source
    ///     is seen from right now.
    /// </summary>
    public void CopySprite(Entity<SpriteComponent> source, Entity<SpriteComponent> copy, Direction direction)
    {
        if (!TryComp(source, out HumanoidAppearanceComponent? humanoid))
        {
            _sprite.CopySprite(source.AsNullable(), copy.AsNullable());
            return;
        }

        var view = GetView(direction);
        var state = GetState(source);
        var sourceView = state.View;
        if (sourceView != view)
            ApplyOrdering((source, humanoid, source.Comp), state, view);

        _sprite.CopySprite(source.AsNullable(), copy.AsNullable());

        // The layout depends only on the view, so the source gets back exactly the layout it had. A source that was
        // never laid out keeps this one until the next frame picks its own view.
        if (sourceView is { } previous && previous != view)
            ApplyOrdering((source, humanoid, source.Comp), state, previous);
    }

    private void OnAppearanceUpdated(EntityUid uid, HumanoidAppearanceComponent component, HumanoidAppearanceUpdatedEvent args)
    {
        if (TryComp(uid, out SpriteComponent? sprite))
            Reapply((uid, component, sprite));
    }

    private void OnEquipmentVisualsUpdated(EquipmentVisualsUpdatedEvent args)
    {
        if (args.Slot != "neck" && args.Slot != "back" ||
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

        var ears = TryGetIndex(ent, HumanoidVisualLayers.Hair, out _) ? GetEarKeys(ent) : new List<object>();
        var tail = GetTailKeys(ent, ears);

        // Only this system moves these blocks, so the first pass still sees the species' native order.
        if (!state.NativeCaptured)
        {
            state.NativeCaptured = true;
            state.TailAnchor = FindLayerAbove(ent, tail);
        }

        var cloakAnchor = GetCloakAnchor(ent);
        var cloak = cloakAnchor != null ? GetSlotKeys(ent, "neck", ears, tail) : new List<object>();

        var blocks = ExtractBlocks(ent, ears, tail, cloak);
        var earLayers = blocks[0];
        var tailLayers = blocks[1];
        var cloakLayers = blocks[2];

        if (cloakLayers.Count > 0 && TryGetIndex(ent, cloakAnchor!, out var cloakAnchorIdx))
            InsertBlock(ent, cloakLayers, cloakAnchorIdx + 1);

        // Ears sit right against the hair block: under it, or over it when the character opts in.
        if (earLayers.Count > 0)
            InsertBlock(ent, earLayers, GetEarIndex(ent));

        if (tailLayers.Count > 0)
            InsertBlock(ent, tailLayers, GetTailIndex(ent, state, view));
    }

    private int GetEarIndex(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        TryGetIndex(ent, HumanoidVisualLayers.Hair, out var hairIdx);
        if (!ent.Comp1.EarsAboveHair)
            return hairIdx;

        var hair = new List<object>();
        AddMarkingLayerKeys(ent, HumanoidVisualLayers.Hair, hair);
        var top = hairIdx;
        foreach (var key in hair)
        {
            if (TryGetIndex(ent, key, out var index))
                top = Math.Max(top, index);
        }

        return top + 1;
    }

    private int? GetTailIndex(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, LayeringState state, DirectionalView view)
    {
        switch (view)
        {
            // Facing the camera the tail points away from it, so it goes behind every layer.
            case DirectionalView.Front:
                return 0;
            case DirectionalView.Side when TryGetLowestLeg(ent, out var legIdx):
                return legIdx;
            // A null result means nothing sat above the tail or that layer is gone, so it goes back on top.
            default:
                return state.TailAnchor != null ? IndexOf(ent.Comp2, state.TailAnchor) : null;
        }
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

    private List<object> GetEarKeys(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        var keys = new List<object>();
        foreach (var markingList in ent.Comp1.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (!_markingManager.TryGetMarking(marking, out var prototype) || !prototype.UnderHair)
                    continue;

                foreach (var sprite in prototype.Sprites)
                {
                    if (sprite is SpriteSpecifier.Rsi rsi)
                        keys.Add(GetMarkingKey(marking.MarkingId, rsi.RsiState));
                }
            }
        }

        return FilterExisting(ent, keys, null, null);
    }

    // Tail markings layered onto TailBehind or TailBehindBackpack (moth wings) split themselves per view already.
    private List<object> GetTailKeys(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, List<object> ears)
    {
        var keys = new List<object> { HumanoidVisualLayers.Tail, HumanoidVisualLayers.Wings };
        AddMarkingLayerKeys(ent, HumanoidVisualLayers.Tail, keys);
        AddMarkingLayerKeys(ent, HumanoidVisualLayers.Wings, keys);
        return FilterExisting(ent, keys, ears, null);
    }

    private List<object> GetSlotKeys(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        string slot,
        List<object> ears,
        List<object> tail)
    {
        var keys = new List<object>();
        if (TryComp(ent.Owner, out InventorySlotsComponent? slots) &&
            slots.VisualLayerKeys.TryGetValue(slot, out var slotKeys))
        {
            keys.AddRange(slotKeys);
        }

        return FilterExisting(ent, keys, ears, tail);
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

    // The layer right above the block in the native order, whatever its key; the block goes back directly below it.
    private ISpriteLayer? FindLayerAbove(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, List<object> block)
    {
        var end = -1;
        foreach (var key in block)
        {
            if (TryGetIndex(ent, key, out var index))
                end = Math.Max(end, index);
        }

        return end < 0 ? null : ent.Comp2.AllLayers.ElementAtOrDefault(end + 1);
    }

    private static int? IndexOf(SpriteComponent sprite, ISpriteLayer layer)
    {
        var index = 0;
        foreach (var candidate in sprite.AllLayers)
        {
            if (candidate == layer)
                return index;

            index++;
        }

        return null;
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

    // 4-directional RSIs round diagonals to north or south, see DirExt.Convert.
    private static DirectionalView GetView(Direction direction)
    {
        return direction switch
        {
            Direction.South or Direction.SouthEast or Direction.SouthWest => DirectionalView.Front,
            Direction.North or Direction.NorthEast or Direction.NorthWest => DirectionalView.Back,
            _ => DirectionalView.Side,
        };
    }
}
