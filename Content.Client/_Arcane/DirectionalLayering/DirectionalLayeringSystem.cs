using System.Collections.Generic;
using Content.Client.Humanoid;
using Content.Client.Inventory;
using Content.Shared.Clothing;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Arcane.DirectionalLayering;

/// <summary>
///     Reorders a humanoid sprite's hair and neck layers based on whether the entity is facing the camera.
///     Species differ in their layer layouts (humans keep "mask" directly above the hair while arachnids keep a
///     whole face cluster between the hair and "HeadTop"), so the block boundaries are discovered from the sprite
///     instead of being hard-coded and the ordering is corrected against the camera rather than the world.
/// </summary>
public sealed class DirectionalLayeringSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, OrderingCache> _cache = new();

    /// <summary>
    ///     Preview dummies are rotated through a SpriteView direction override instead of the entity transform, so
    ///     their facing has to be tracked separately to survive reloads.
    /// </summary>
    private readonly Dictionary<EntityUid, DirectionalView> _dummyViews = new();

    private Angle _lastEyeRotation = Angle.Zero;

    private static readonly ProtoId<SpeciesPrototype> HarpySpecies = "Harpy";

    /// <summary>
    ///     Per-entity snapshot of the layers that make up the hair, neck and tail blocks, plus the per-entity markers
    ///     used when moving those blocks between the species' default and camera-facing layouts.
    /// </summary>
    private sealed class OrderingCache
    {
        public List<object> HairKeys = new();
        public object? HairFrontAnchor;
        public List<object> CloakKeys = new();
        public List<object> TailKeys = new();
        public object? TailAnchor;
        public bool TailAnchorCaptured;
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

        SubscribeLocalEvent<HumanoidAppearanceComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HumanoidAppearanceComponent, HumanoidAppearanceUpdatedEvent>(OnAppearanceUpdated);
        SubscribeLocalEvent<EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ComponentRemove>(OnRemove);
    }

    public override void Shutdown()
    {
        _cache.Clear();
        _dummyViews.Clear();
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        var eyeRotation = _eyeManager.CurrentEye.Rotation;
        if (eyeRotation.EqualsApprox(_lastEyeRotation))
            return;

        _lastEyeRotation = eyeRotation;

        var query = EntityQueryEnumerator<HumanoidAppearanceComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var humanoid, out var sprite, out _))
        {
            ApplyOrdering((uid, humanoid, sprite));
        }
    }

    /// <summary>
    ///     Applies the layer ordering for a preview dummy whose facing is set through a SpriteView direction
    ///     override instead of the entity transform. The view is recomputed against the given direction rather than
    ///     the world/eye rotation, so rotating a dummy in the editor reorders its layers like in-game.
    /// </summary>
    public void ApplyDummyOrdering(EntityUid uid, Direction direction)
    {
        if (!TryComp(uid, out HumanoidAppearanceComponent? humanoid) ||
            !TryComp(uid, out SpriteComponent? sprite))
        {
            return;
        }

        _dummyViews[uid] = GetView(direction);
        ApplyOrdering((uid, humanoid, sprite));
    }

    private void OnMove(EntityUid uid, HumanoidAppearanceComponent component, ref MoveEvent args)
    {
        if (args.OldRotation.GetCardinalDir() == args.NewRotation.GetCardinalDir())
            return;

        if (TryComp(uid, out SpriteComponent? sprite))
            ApplyOrdering((uid, component, sprite));
    }

    private void OnStartup(EntityUid uid, HumanoidAppearanceComponent component, ComponentStartup args)
    {
        if (TryComp(uid, out SpriteComponent? sprite))
            ApplyOrdering((uid, component, sprite));
    }

    private void OnAppearanceUpdated(EntityUid uid, HumanoidAppearanceComponent component, HumanoidAppearanceUpdatedEvent args)
    {
        if (TryComp(uid, out SpriteComponent? sprite))
            ApplyOrdering((uid, component, sprite));
    }

    private void OnEquipmentVisualsUpdated(EquipmentVisualsUpdatedEvent args)
    {
        if (args.Slot != "neck" && args.Slot != "back" ||
            !TryComp(args.Equipee, out HumanoidAppearanceComponent? humanoid) ||
            !TryComp(args.Equipee, out SpriteComponent? sprite))
        {
            return;
        }

        ApplyOrdering((args.Equipee, humanoid, sprite));
    }

    private void OnRemove(EntityUid uid, HumanoidAppearanceComponent component, ComponentRemove args)
    {
        _cache.Remove(uid);
        _dummyViews.Remove(uid);
    }

    private void ApplyOrdering(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        if (!TryComp(ent.Owner, out TransformComponent? xform))
            return;

        // Preview dummies are turned through a SpriteView direction override, so their facing is recovered from the
        // tracked dummy view; in-game entities are faced relative to the camera instead of the absolute world.
        // The renderer picks the RSI direction from `worldRotation + eyeRotation`, so the facing has to be
        // determined relative to the camera, not the absolute world rotation.
        var view = _dummyViews.TryGetValue(ent.Owner, out var dummyView)
            ? dummyView
            : GetView(_transform.GetWorldRotation(ent.Owner) + _eyeManager.CurrentEye.Rotation);
        var cache = GetCache(ent);

        // Tail and cloak first, so the back view's "hair between the head-trim cluster and the tail" layout gets
        // the tail above the hair without the two blocks fighting over the same index.
        EnsureTailAndCloakLayout(ent, view, cache);
        EnsureHairLayout(ent, view == DirectionalView.Back, cache);
    }

    private bool TryGetLayerIndex(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, object key, out int index)
    {
        var sprite = ent.Comp2;
        index = 0;
        return key switch
        {
            Enum enumKey => _sprite.LayerMapTryGet((ent.Owner, sprite), enumKey, out index, false),
            string stringKey => _sprite.LayerMapTryGet((ent.Owner, sprite), stringKey, out index, false),
            _ => false,
        };
    }

    private void SetLayerIndex(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, object key, int index)
    {
        var sprite = ent.Comp2;
        switch (key)
        {
            case Enum enumKey:
                _sprite.LayerMapSet((ent.Owner, sprite), enumKey, index);
                break;
            case string stringKey:
                _sprite.LayerMapSet((ent.Owner, sprite), stringKey, index);
                break;
        }
    }

    /// <summary>
    ///     Gets or rebuilds the block snapshot for the entity. Memberships are re-derived whenever the marking or
    ///     neck layout differs from what is cached; anchors are invalidated together with the membership.
    /// </summary>
    private OrderingCache GetCache(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        var hairKeys = UniqueKeys(GetHairBlockKeys(ent));
        var cloakKeys = UniqueKeys(GetCloakBlockKeys(ent));
        var tailKeys = UniqueKeys(GetTailBlockKeys(ent));

        if (_cache.TryGetValue(ent.Owner, out var cache) &&
            SameKeys(cache.HairKeys, hairKeys) &&
            SameKeys(cache.CloakKeys, cloakKeys) &&
            SameKeys(cache.TailKeys, tailKeys))
        {
            return cache;
        }

        _cache.TryGetValue(ent.Owner, out var previous);
        cache = new OrderingCache
        {
            HairKeys = hairKeys,
            CloakKeys = cloakKeys,
            TailKeys = tailKeys,
            // The species-level anchor the tail rests above never changes with the markings, so keep it across
            // membership rebuilds once it has been captured from a native (base) ordering.
            TailAnchor = previous?.TailAnchor,
            TailAnchorCaptured = previous?.TailAnchorCaptured ?? false,
        };
        _cache[ent.Owner] = cache;
        return cache;
    }

    private static bool SameKeys(List<object> a, List<object> b)
    {
        if (a.Count != b.Count)
            return false;

        foreach (var key in a)
        {
            var found = false;
            foreach (var other in b)
            {
                if (Equals(key, other))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
        }

        return true;
    }

    private static List<object> UniqueKeys(List<object> keys)
    {
        var unique = new List<object>(keys.Count);
        foreach (var key in keys)
        {
            var found = false;
            foreach (var other in unique)
            {
                if (Equals(key, other))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                unique.Add(key);
        }

        return unique;
    }

    private List<object> GetHairBlockKeys(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        var hairKeys = new List<object> { HumanoidVisualLayers.Hair };

        foreach (var markingList in ent.Comp1.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (!_markingManager.TryGetMarking(marking, out var prototype) ||
                    prototype.MarkingCategory != MarkingCategories.Hair)
                {
                    continue;
                }

                foreach (var sprite in prototype.Sprites)
                {
                    if (sprite is SpriteSpecifier.Rsi rsi)
                        hairKeys.Add($"{marking.MarkingId}-{rsi.RsiState}");
                }
            }
        }

        return hairKeys;
    }

    private List<object> GetCloakBlockKeys(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        var cloakKeys = new List<object>();
        if (TryComp(ent.Owner, out InventorySlotsComponent? slots) &&
            slots.VisualLayerKeys.TryGetValue("neck", out var neckKeys))
        {
            cloakKeys.AddRange(neckKeys);
        }

        return cloakKeys;
    }

    /// <summary>
    ///     The tail/wings visual layers and their marking layers, restricted to keys the sprite actually has (the
    ///     "Wings" visual layer only exists for species that define it).
    /// </summary>
    private List<object> GetTailBlockKeys(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        var tailKeys = new List<object>
        {
            HumanoidVisualLayers.Tail,
            HumanoidVisualLayers.Wings,
        };

        foreach (var (category, markings) in ent.Comp1.MarkingSet.Markings)
        {
            if (category != MarkingCategories.Tail && category != MarkingCategories.Wings)
                continue;

            foreach (var marking in markings)
            {
                if (!_markingManager.TryGetMarking(marking, out var prototype))
                    continue;

                foreach (var sprite in prototype.Sprites)
                {
                    if (sprite is SpriteSpecifier.Rsi rsi)
                        tailKeys.Add($"{marking.MarkingId}-{rsi.RsiState}");
                }
            }
        }

        for (var i = tailKeys.Count - 1; i >= 0; i--)
        {
            if (!TryGetLayerIndex(ent, tailKeys[i], out _))
                tailKeys.RemoveAt(i);
        }

        return tailKeys;
    }

    private void EnsureHairLayout(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        bool backView,
        OrderingCache cache)
    {
        if (cache.HairKeys.Count == 0 ||
            !TryGetClusterTop(ent, out var clusterTop, out var clusterTopIdx))
        {
            return;
        }

        if (backView)
        {
            // The back of the head goes over the ears/head-top cluster: hair lands directly above it.
            TryMoveBlockRelative(ent, cache.HairKeys, clusterTop, true);
            return;
        }

        if (cache.HairFrontAnchor is not { } anchor)
        {
            // No anchor known and the hair is stuck above the whole cluster (e.g. markings changed during a
            // back view). Reset it directly below the mask, or below the HeadTop base when no mask is worn, so
            // the front anchor can be re-discovered on the next pass.
            if (!TryGetBlockExtent(ent, cache.HairKeys, out _, out var blockEnd))
                return;

            if (blockEnd > clusterTopIdx)
            {
                object resetAnchorKey = "mask";
                var hasResetAnchor = TryGetLayerIndex(ent, "mask", out _);
                if (!hasResetAnchor)
                {
                    resetAnchorKey = HumanoidVisualLayers.HeadTop;
                    hasResetAnchor = TryGetLayerIndex(ent, HumanoidVisualLayers.HeadTop, out _);
                }

                if (hasResetAnchor)
                    TryMoveBlockRelative(ent, cache.HairKeys, resetAnchorKey, false);

                return;
            }

            // First contact with the default front layout: remember which head-trim base layer sits right above
            // the hair. HeadTop/HeadSide marking layers are excluded so the anchor can never become an ear
            // marking interleaved with the hair block.
            if (TryGetFrontAnchor(ent, blockEnd, out var frontAnchor))
                cache.HairFrontAnchor = frontAnchor;

            return;
        }

        // Front face visible: hair below the marker that the species puts above it.
        TryMoveBlockRelative(ent, cache.HairKeys, anchor, false);
    }

    /// <summary>
    ///     Reorders the tail/wings block against the cloak (neck) block per view. From the back the tail is native:
    ///     it rests on its species anchor (usually the head-trim cluster top, or the head itself for species whose
    ///     base order puts the tail above the head) with the cloak tucked directly below it, so the tail hides the
    ///     cloak. From the front and the sides the cloak spreads over the shoulders directly below the head and the
    ///     tail is tucked under the cloak, so the tail only renders above the cloak when the back faces the camera.
    /// </summary>
    private void EnsureTailAndCloakLayout(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        DirectionalView view,
        OrderingCache cache)
    {
        if (cache.CloakKeys.Count == 0 ||
            cache.TailKeys.Count == 0 ||
            !TryGetLayerIndex(ent, "head", out _) ||
            !TryResolveBlock(ent, cache.TailKeys, out var tailIndices, out var tailStart, out _) ||
            !TryResolveBlock(ent, cache.CloakKeys, out _, out _, out var cloakTop))
        {
            return;
        }

        // The native base layout has the cloak below the tail, which is also the back view's layout.
        var cloakBelowTail = cloakTop < tailStart;
        if (!cache.TailAnchorCaptured && cloakBelowTail)
        {
            if (TryGetTailAnchor(ent, tailStart, out var anchor, out _))
            {
                cache.TailAnchor = anchor;
                cache.TailAnchorCaptured = true;
            }
        }

        var wantCloakBelowTail = view == DirectionalView.Back;

        if (ent.Comp1.Species == HarpySpecies)
        {
            // Harpies draw big back wings on the Tail layer above the head in their base order, so the cloak (neck)
            // is already below them on every view; keep the native layout rather than burying the wings under it.
            return;
        }

        // A backpack (or any other back-slot item) renders above the cloak on every view, so even when the back view
        // already has the cloak below the tail, fall through to lower the cloak below the "back" layer if it is up.
        var cloakBelowBack =
            !TryGetLayerIndex(ent, "back", out var backIdx) ||
            cloakTop < backIdx;

        if (cloakBelowTail == wantCloakBelowTail && (view != DirectionalView.Back || cloakBelowBack))
            return;

        var tailCount = cache.TailKeys.Count;
        var cloakCount = cache.CloakKeys.Count;

        // The tail is extracted before anything else mutates the sprite, so its pre-resolved indices from above stay
        // valid; the cloak is re-resolved by the extractor because removing the tail shifts every cloak layer above
        // it down.
        if (!TryExtractResolved(ent, tailIndices, out var tailBlock))
            return;

        if (!TryExtractBlock(ent, cache.CloakKeys, out var cloakBlock, out var cloakStart))
        {
            // The cloak failed to come out; put the tail back where it was rather than dropping it from the sprite.
            InsertBlock(ent, tailBlock, tailStart);
            return;
        }

        if (wantCloakBelowTail)
        {
            // Back view: tail back on its native spot, directly above its anchor; cloak right below the tail, but
            // never at or above the backpack ("back") layer.
            if (cache.TailAnchorCaptured && TryGetLayerIndex(ent, cache.TailAnchor!, out var anchorIdx))
            {
                var backTailTarget = anchorIdx + 1;
                InsertBlock(ent, tailBlock, backTailTarget);
                var backCloakTarget = Math.Min(backTailTarget - cloakCount, GetCloakCeiling(ent));
                InsertBlock(ent, cloakBlock, backCloakTarget);
                return;
            }

            // Anchor unknown (rare, e.g. the cache was rebuilt mid-front-view): fall back to the head-trim cluster.
            if (TryGetClusterTop(ent, out _, out var clusterIdx))
            {
                var backTailTarget = clusterIdx + 1;
                InsertBlock(ent, tailBlock, backTailTarget);
                var backCloakTarget = Math.Min(backTailTarget - cloakCount, GetCloakCeiling(ent));
                InsertBlock(ent, cloakBlock, backCloakTarget);
                return;
            }

            InsertBlock(ent, tailBlock, tailStart);
            InsertBlock(ent, cloakBlock, cloakStart);
            return;
        }

        // Front/side view: cloak spread below the head, tail tucked directly under the cloak; the cloak never goes
        // at or above the backpack ("back") layer.
        if (!TryGetLayerIndex(ent, "head", out var headIdx))
        {
            InsertBlock(ent, tailBlock, tailStart);
            InsertBlock(ent, cloakBlock, cloakStart);
            return;
        }

        var cloakTarget = Math.Min(headIdx - cloakCount, GetCloakCeiling(ent));
        var tailTarget = Math.Max(0, cloakTarget - tailCount);
        // Cloak goes in first: it targets the "back" bookmark's slot, and the tail sits below it. Inserting the tail
        // first would shift the belt/outerClothing layers up onto the cloak's index, drawing them over it.
        InsertBlock(ent, cloakBlock, cloakTarget);
        InsertBlock(ent, tailBlock, tailTarget);
    }

    /// <summary>
    ///     The current top of the head-trim cluster: the highest of the "mask", "HeadSide" and "HeadTop" base layers
    ///     and any HeadTop/HeadSide marking layers (e.g. ears). The hair block is moved above this cluster when the
    ///     back of the head faces the camera, so the ears end up under the hair instead of floating over it.
    /// </summary>
    private bool TryGetClusterTop(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        out object key,
        out int index)
    {
        key = HumanoidVisualLayers.HeadTop;
        index = 0;
        var found = false;

        foreach (var candidate in GetHeadTrimClusterKeys(ent))
        {
            if (!TryGetLayerIndex(ent, candidate, out var candidateIdx))
                continue;

            if (!found || candidateIdx > index)
            {
                found = true;
                key = candidate;
                index = candidateIdx;
            }
        }

        return found;
    }

    /// <summary>
    ///     The base and marking layers that form the head-trim cluster above the hair: "mask", "HeadSide", "HeadTop"
    ///     and their marking layers. When a character opts in to ears-above-hair, the ear marking layers are excluded
    ///     from the cluster so the back-view hair placement lands below them instead of covering them.
    /// </summary>
    private List<object> GetHeadTrimClusterKeys(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        var candidates = new List<object>
        {
            "mask",
            HumanoidVisualLayers.HeadSide,
            HumanoidVisualLayers.HeadTop,
        };

        if (ent.Comp1.EarsAboveHair)
            return candidates;

        foreach (var (category, markings) in ent.Comp1.MarkingSet.Markings)
        {
            if (category != MarkingCategories.HeadTop && category != MarkingCategories.HeadSide)
                continue;

            foreach (var marking in markings)
            {
                if (!_markingManager.TryGetMarking(marking, out var prototype))
                    continue;

                foreach (var sprite in prototype.Sprites)
                {
                    if (sprite is SpriteSpecifier.Rsi rsi)
                        candidates.Add($"{marking.MarkingId}-{rsi.RsiState}");
                }
            }
        }

        return candidates;
    }

    /// <summary>
    ///     The nearest of the "mask", "HeadSide" and "HeadTop" base layers above the given index. Marking layers are
    ///     excluded so the front hair anchor can never fall on an ear marking that sits inside the hair's z-range.
    /// </summary>
    private bool TryGetFrontAnchor(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        int above,
        out object anchor)
    {
        anchor = null!;
        var best = int.MaxValue;
        object bestKey = null!;

        if (TryGetLayerIndex(ent, HumanoidVisualLayers.HeadSide, out var index) &&
            index > above && index < best)
        {
            best = index;
            bestKey = HumanoidVisualLayers.HeadSide;
        }

        if (TryGetLayerIndex(ent, HumanoidVisualLayers.HeadTop, out index) &&
            index > above && index < best)
        {
            best = index;
            bestKey = HumanoidVisualLayers.HeadTop;
        }

        if (TryGetLayerIndex(ent, "mask", out index) &&
            index > above && index < best)
        {
            best = index;
            bestKey = "mask";
        }

        if (best == int.MaxValue)
            return false;

        anchor = bestKey;
        return true;
    }

    /// <summary>
    ///     The species-level layers a tail/wings block can rest directly above in the native base ordering. Species
    ///     differ: most put the tail just above the head-trim cluster, harpies put it above the head itself.
    /// </summary>
    private static readonly object[] TailAnchorCandidates =
    {
        "head",
        HumanoidVisualLayers.HeadTop,
        HumanoidVisualLayers.HeadSide,
        "maskalt",
        "mask",
        HumanoidVisualLayers.FacialHair,
        HumanoidVisualLayers.SnoutCover,
        "neck",
        HumanoidVisualLayers.TailOversuit,
        "back",
        "belt",
        "outerClothing",
        HumanoidVisualLayers.Eyes,
        "ears",
        HumanoidVisualLayers.Snout,
        "id",
    };

    /// <summary>
    ///     The candidate layer with the highest index strictly below <paramref name="below"/>: the layer the current
    ///     tail block rests above.
    /// </summary>
    private bool TryGetTailAnchor(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        int below,
        out object key,
        out int index)
    {
        key = null!;
        index = int.MinValue;
        var found = false;

        foreach (var candidate in TailAnchorCandidates)
        {
            if (!TryGetLayerIndex(ent, candidate, out var candidateIdx) || candidateIdx >= below)
                continue;

            if (!found || candidateIdx > index)
            {
                found = true;
                key = candidate;
                index = candidateIdx;
            }
        }

        return found;
    }

    /// <summary>
    ///     The index at which the cloak block may start so it lands directly below the backpack ("back") layer:
    ///     inserting there pushes the backpack (and everything above it) up, keeping it above the cloak while the
    ///     cloak stays above whatever sits below the backpack (belt, outer clothing). Only applied while a backpack
    ///     is actually worn; the static "back" bookmark exists on every humanoid even when empty. Returns the current
    ///     layer count otherwise.
    /// </summary>
    private int GetCloakCeiling(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        // The backpack always renders above the cloak: the cloak block is inserted at the "back" layer's index.
        // A backIdx - cloakCount target would land on the belt's slot, shifting the belt above the cloak.
        if (TryComp(ent.Owner, out InventorySlotsComponent? slots) &&
            slots.VisualLayerKeys.TryGetValue("back", out var backKeys) &&
            backKeys.Count > 0 &&
            TryGetLayerIndex(ent, "back", out var backIdx))
        {
            return Math.Max(0, backIdx);
        }

        return int.MaxValue;
    }

    private bool TryGetBlockExtent(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        List<object> keys,
        out int start,
        out int end)
    {
        start = int.MaxValue;
        end = int.MinValue;

        foreach (var key in keys)
        {
            if (!TryGetLayerIndex(ent, key, out var index))
                return false;

            start = Math.Min(start, index);
            end = Math.Max(end, index);
        }

        return true;
    }

    /// <summary>
    ///     Resolves the block's keys to their current layer indices in a single pass. The indices go stale the moment
    ///     the sprite changes (e.g. an insertion below the block), so callers must re-resolve after mutating it.
    /// </summary>
    private bool TryResolveBlock(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        List<object> keys,
        out List<(object Key, int Index)> indices,
        out int start,
        out int end)
    {
        indices = new List<(object Key, int Index)>(keys.Count);
        start = int.MaxValue;
        end = int.MinValue;

        foreach (var key in keys)
        {
            if (!TryGetLayerIndex(ent, key, out var index))
                return false;

            start = Math.Min(start, index);
            end = Math.Max(end, index);
            indices.Add((key, index));
        }

        return true;
    }

    /// <summary>
    ///     Captures the block's layers out of the sprite, keeping their relative order, and removes them. The indices
    ///     must be resolved against the sprite's current state (see <see cref="TryResolveBlock"/>).
    /// </summary>
    private bool TryExtractResolved(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        List<(object Key, int Index)> indices,
        out List<(object Key, Layer Layer)> block)
    {
        var sprite = ent.Comp2;
        block = new List<(object, Layer)>(indices.Count);

        // Remove from the top-most layer down so the captured indices stay valid while removing. Duplicate keys
        // (e.g. the same marking listed twice) resolve to the same layer index, so compact adjacent duplicates
        // after sorting: removing the same index twice would delete the layer directly above the block (a clothing
        // layer, say) and wipe its key from the layer map. `indices` and `block` stay aligned for the rollback.
        indices.Sort((a, b) => b.Index.CompareTo(a.Index));

        var kept = new List<(object Key, int Index)>(indices.Count);
        foreach (var entry in indices)
        {
            if (kept.Count != 0 && kept[^1].Index == entry.Index)
                continue;

            kept.Add(entry);
        }

        indices = kept;

        for (var i = 0; i < indices.Count; i++)
        {
            if (!_sprite.RemoveLayer((ent.Owner, sprite), indices[i].Index, out var layer, false))
            {
                for (var j = block.Count - 1; j >= 0; j--)
                {
                    _sprite.AddLayer((ent.Owner, sprite), block[j].Layer, indices[j].Index);
                    SetLayerIndex(ent, block[j].Key, indices[j].Index);
                }

                block.Clear();
                return false;
            }

            block.Add((indices[i].Key, layer!));
        }

        // block was built from top to bottom; restore the on-screen draw order.
        block.Reverse();
        return true;
    }

    /// <summary>
    ///     Resolves the block's keys and removes its layers, keeping the on-screen draw order.
    /// </summary>
    private bool TryExtractBlock(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        List<object> keys,
        out List<(object Key, Layer Layer)> block,
        out int start)
    {
        if (!TryResolveBlock(ent, keys, out var indices, out start, out _))
        {
            block = null!;
            return false;
        }

        return TryExtractResolved(ent, indices, out block);
    }

    /// <summary>
    ///     Inserts the previously extracted block so it occupies <paramref name="targetStart"/> onward, and re-registers
    ///     every layer map key that belongs to it.
    /// </summary>
    private void InsertBlock(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        List<(object Key, Layer Layer)> block,
        int targetStart)
    {
        var sprite = ent.Comp2;

        for (var i = 0; i < block.Count; i++)
        {
            _sprite.AddLayer((ent.Owner, sprite), block[i].Layer, targetStart + i);
            SetLayerIndex(ent, block[i].Key, targetStart + i);
        }
    }

    /// <summary>
    ///     Moves a contiguous block of layers so it rests directly across a single anchor layer in one view: its start
    ///     landing on <c>anchorEnd + 1</c> (above the anchor, e.g. the tail over the legs from the back), or its end
    ///     landing on <c>anchorStart - 1</c> (below the anchor, e.g. the tail under the feet from the front).
    /// </summary>
    /// <summary>
    ///     Moves a contiguous block of layers so it rests directly across a single anchor layer in one view: its start
    ///     landing on <c>anchorEnd + 1</c> (directly above the anchor, e.g. the hair over the head-trim cluster), or its
    ///     end landing on <c>anchorStart - 1</c> (directly below the anchor, e.g. the hair under the front marker). The
    ///     block's keys are resolved a single time and reused both for the already-correct check and for the removal
    ///     pass; the anchor is re-resolved after the extraction because inserting the block back shifts the layers
    ///     above it. Nothing is touched when the block already sits where this view wants it.
    /// </summary>
    private bool TryMoveBlockRelative(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        List<object> blockKeys,
        object anchorLayer,
        bool placeAbove)
    {
        // Single resolution pass; the same indices drive the already-correct check and the removal below.
        if (!TryResolveBlock(ent, blockKeys, out var indices, out var blockStart, out var blockEnd))
            return false;

        if (!TryGetLayerIndex(ent, anchorLayer, out var anchorStart))
            return false;

        // Already in the layout this view wants.
        if (placeAbove ? blockStart == anchorStart + 1 : blockEnd == anchorStart - 1)
            return true;

        // Pull the block out (using the resolved indices), then drop it next to its anchor. Re-resolving the anchor
        // after the removal is load-bearing: removing the block shifts every layer above it down by its count.
        if (!TryExtractResolved(ent, indices, out var block))
            return false;

        if (!TryGetLayerIndex(ent, anchorLayer, out anchorStart))
        {
            // The anchor vanished mid-reorder (e.g. a foot lost while animating); put the block back rather than
            // dropping it from the sprite.
            InsertBlock(ent, block, blockStart);
            return false;
        }

        InsertBlock(ent, block, placeAbove ? anchorStart + 1 : Math.Max(0, anchorStart - block.Count));
        return true;
    }

    /// <summary>
    ///     Mirrors <see cref="Layer.GetDirection"/> for 4-directional layers (what humanoid body parts use), including
    ///     the anti-flicker direction bias, so this system's ordering matches the sprite states that actually render.
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

    /// <summary>
    ///     Which view a humanoid sprite displays when rendered towards the given cardinal direction, matching how
    ///     the renderer converts a direction override into an RSI direction.
    /// </summary>
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
