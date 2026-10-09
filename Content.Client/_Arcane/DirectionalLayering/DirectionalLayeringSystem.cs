using System.Collections.Generic;
using Content.Client.Humanoid;
using Content.Client.Inventory;
using Content.Shared.Clothing;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
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
    ///     The view last applied to each entity. FrameUpdate skips entities whose view has not changed, so a steadily
    ///     rotating camera does not re-run the block-key discovery (marking lookups, block extent checks) for faces
    ///     that stay on the same side of the character. Appearance/equipment changes invalidate the stored view.
    /// </summary>
    private readonly Dictionary<EntityUid, DirectionalView> _lastViews = new();

    /// <summary>
    ///     Preview dummies are rotated through a SpriteView direction override instead of the entity transform, so
    ///     their facing has to be tracked separately to survive reloads.
    /// </summary>
    private readonly Dictionary<EntityUid, DirectionalView> _dummyViews = new();

    private Angle _lastEyeRotation = Angle.Zero;

    /// <summary>
    ///     Scratch buffers the block-key discovery fills instead of allocating fresh lists. <see cref="GetCache"/>
    ///     runs on every reorder, so new lists plus fresh key strings churned garbage for every humanoid on every
    ///     facing change even when the keys turned out to be unchanged. The buffers are only ever read into a
    ///     <see cref="OrderingCache"/> that has actually changed.
    /// </summary>
    private readonly List<object> _hairKeyScratch = new();
    private readonly List<object> _cloakKeyScratch = new();
    private readonly List<object> _tailKeyScratch = new();
    private readonly Dictionary<(string MarkingId, string RsiState), string> _markingKeyCache = new();

    private static readonly ProtoId<SpeciesPrototype> HarpySpecies = "Harpy";
    private static readonly object[] LegLayerCandidates =
    {
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RFoot,
        HumanoidVisualLayers.LFoot,
    };

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
        _lastViews.Clear();
        _dummyViews.Clear();
        _markingKeyCache.Clear();
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
            // The eye rotation moves smoothly while the RSI view only changes on cardinal steps, so most frames
            // resolve to the same facing. Skip the (marking-lookup heavy) reordering until the facing actually
            // changes; the data-change subscriptions invalidate the stored view explicitly.
            var view = GetActiveView(uid);
            if (_lastViews.TryGetValue(uid, out var last) && last == view)
                continue;

            _lastViews[uid] = view;
            ApplyOrdering((uid, humanoid, sprite), view);
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

        var view = GetView(direction);
        _dummyViews[uid] = view;
        _lastViews.Remove(uid);
        ApplyOrdering((uid, humanoid, sprite), view);
    }

    private void OnMove(EntityUid uid, HumanoidAppearanceComponent component, ref MoveEvent args)
    {
        // The facing is camera-relative (world rotation + eye), so a turn that stays on one cardinal step can
        // still cross a view boundary once the eye is turned: e.g. a 0° → 20° rotation under a 30° camera is
        // Front → Side, even though both angles are cardinal-south. Compare the computed camera-relative views
        // for the old and new world rotation instead of bailing on equal cardinal directions.
        var worldRotation = _transform.GetWorldRotation(uid);
        var oldWorldRotation = worldRotation - (args.NewRotation - args.OldRotation);
        var eyeRotation = _eyeManager.CurrentEye.Rotation;
        if (GetView(worldRotation + eyeRotation) == GetView(oldWorldRotation + eyeRotation))
            return;

        // Movement on its own does not change the block keys, but the facing is derived from world rotation, so
        // drop the saved view and let the reorder re-resolve it rather than trusting a stale front/side/back.
        _lastViews.Remove(uid);

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
        // New markings or hairstyles change the hair/tail block keys, so the cached layout no longer reflects the
        // sprite: force a full reorder even if the facing looks unchanged.
        _lastViews.Remove(uid);

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

        // Neck/back gear changes the cloak block keys, so the cached layout is stale: force a full reorder.
        _lastViews.Remove(args.Equipee);
        ApplyOrdering((args.Equipee, humanoid, sprite));
    }

    private void OnRemove(EntityUid uid, HumanoidAppearanceComponent component, ComponentRemove args)
    {
        _cache.Remove(uid);
        _lastViews.Remove(uid);
        _dummyViews.Remove(uid);
    }

    private void ApplyOrdering(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        var view = GetActiveView(ent.Owner);
        _lastViews[ent.Owner] = view;
        ApplyOrdering(ent, view);
    }

    private void ApplyOrdering(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, DirectionalView view)
    {
        var cache = GetCache(ent);

        if (view == DirectionalView.Back)
            EnsureTailAtNativeAnchor(ent, cache);

        EnsureTailAndCloakLayout(ent, view, cache);
        if (view != DirectionalView.Back)
            EnsureTailBehindLegs(ent, cache, view);
        EnsureHairLayout(ent, view == DirectionalView.Back, cache);

        if (view == DirectionalView.Back)
            EnsureTailAboveHair(ent, cache);
    }

    /// <summary>
    ///     The camera-relative view for an entity: the tracked dummy override when this is a preview dummy, or the
    ///     facing derived from its world rotation against the current eye otherwise. Preview dummies are turned
    ///     through a SpriteView direction override, so their facing is recovered from the tracked dummy view;
    ///     in-game entities are faced relative to the camera instead of the absolute world. The renderer picks the
    ///     RSI direction from <c>worldRotation + eyeRotation</c>, so the facing has to be determined relative to the
    ///     camera, not the absolute world rotation.
    /// </summary>
    private DirectionalView GetActiveView(EntityUid uid)
    {
        if (_dummyViews.TryGetValue(uid, out var dummyView))
            return dummyView;

        return GetView(_transform.GetWorldRotation(uid) + _eyeManager.CurrentEye.Rotation);
    }

    /// <summary>
    ///     Undo the front/side tail placement before applying the existing tail/cloak ordering for the back view.
    /// </summary>
    private void EnsureTailAtNativeAnchor(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, OrderingCache cache)
    {
        if (cache.TailKeys.Count == 0 || ent.Comp1.Species == HarpySpecies)
            return;

        if (cache.TailAnchorCaptured && TryGetLayerIndex(ent, cache.TailAnchor!, out _))
        {
            TryMoveBlockRelative(ent, cache.TailKeys, cache.TailAnchor!, true);
            return;
        }

        if (TryGetClusterTop(ent, out var clusterTop, out _))
            TryMoveBlockRelative(ent, cache.TailKeys, clusterTop, true);
    }

    /// <summary>
    ///     Puts the tail out of sight based on the view. From the front (South) the tail points away from the camera,
    ///     so it is sunk to the very bottom of the sprite (index 0), behind every layer. In the side views it still
    ///     hangs behind the body, so the complete tail/wings block is anchored before the first visible leg layer.
    /// </summary>
    private void EnsureTailBehindLegs(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        OrderingCache cache,
        DirectionalView view)
    {
        if (cache.TailKeys.Count == 0 || ent.Comp1.Species == HarpySpecies)
            return;

        if (view == DirectionalView.Front)
        {
            if (!TryResolveBlock(ent, cache.TailKeys, out var frontIndices, out var frontStart, out var frontEnd))
                return;

            if (frontStart == 0 && IsResolvedBlockContiguous(frontIndices, frontStart, frontEnd))
                return;

            if (!TryExtractResolved(ent, frontIndices, out var frontBlock))
                return;

            InsertBlock(ent, frontBlock, 0);
            return;
        }

        object? firstLeg = null;
        var firstLegIndex = int.MaxValue;
        foreach (var leg in LegLayerCandidates)
        {
            if (TryGetLayerIndex(ent, leg, out var index) && index < firstLegIndex)
            {
                firstLeg = leg;
                firstLegIndex = index;
            }
        }

        if (firstLeg != null)
            TryMoveBlockRelative(ent, cache.TailKeys, firstLeg, false);
    }

    /// <summary>
    ///     In the back view the tail block must sit above the hair block. The hair is hoisted above the head-trim
    ///     cluster, and for species whose native tail anchor sits below that cluster the tail would otherwise be
    ///     left underneath the hair. Re-anchors the tail directly above the hair block when needed.
    /// </summary>
    private void EnsureTailAboveHair(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, OrderingCache cache)
    {
        if (cache.TailKeys.Count == 0 ||
            cache.HairKeys.Count == 0 ||
            ent.Comp1.Species == HarpySpecies)
        {
            return;
        }

        if (!TryResolveBlock(ent, cache.TailKeys, out var tailIndices, out var tailStart, out var tailEnd) ||
            !TryGetBlockExtent(ent, cache.HairKeys, out _, out var hairEnd))
        {
            return;
        }

        // Already above the whole hair block.
        if (IsResolvedBlockContiguous(tailIndices, tailStart, tailEnd) && tailStart > hairEnd)
            return;

        if (!TryExtractResolved(ent, tailIndices, out var tailBlock))
            return;

        // Re-resolve the hair extent: pulling the tail out may have shifted the hair block down.
        if (!TryGetBlockExtent(ent, cache.HairKeys, out _, out var refreshedHairEnd))
        {
            InsertBlock(ent, tailBlock, tailStart);
            return;
        }

        InsertBlock(ent, tailBlock, refreshedHairEnd + 1);
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
        var hairKeys = UniqueKeys(GetHairBlockKeys(ent, _hairKeyScratch));
        var cloakKeys = UniqueKeys(GetCloakBlockKeys(ent, _cloakKeyScratch));
        var tailKeys = UniqueKeys(GetTailBlockKeys(ent, _tailKeyScratch));

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
            HairKeys = new List<object>(hairKeys),
            CloakKeys = new List<object>(cloakKeys),
            TailKeys = new List<object>(tailKeys),
            // The species-level anchor the tail rests above never changes with the markings, so keep it across
            // membership rebuilds once it has been captured from a native (base) ordering.
            TailAnchor = previous?.TailAnchor,
            TailAnchorCaptured = previous?.TailAnchorCaptured ?? false,
        };

        // Save the tail's native anchor before directional ordering changes its layer index.
        if (!cache.TailAnchorCaptured &&
            TryResolveBlock(ent, cache.TailKeys, out _, out var tailStart, out _) &&
            TryGetTailAnchor(ent, tailStart, out var anchor, out _))
        {
            cache.TailAnchor = anchor;
            cache.TailAnchorCaptured = true;
        }

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

    /// <summary>
    ///     Drops duplicate keys in place and returns the same buffer, so the caller can keep reusing it.
    /// </summary>
    private static List<object> UniqueKeys(List<object> keys)
    {
        var unique = 0;
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var found = false;
            for (var j = 0; j < unique; j++)
            {
                if (!Equals(keys[j], key))
                    continue;

                found = true;
                break;
            }

            if (found)
                continue;

            keys[unique] = key;
            unique++;
        }

        keys.RemoveRange(unique, keys.Count - unique);
        return keys;
    }

    private List<object> GetHairBlockKeys(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, List<object> buffer)
    {
        buffer.Clear();
        buffer.Add(HumanoidVisualLayers.Hair);

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
                        buffer.Add(GetMarkingKey(marking.MarkingId, rsi.RsiState));
                }
            }
        }

        return buffer;
    }

    private List<object> GetCloakBlockKeys(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, List<object> buffer)
    {
        buffer.Clear();
        if (TryComp(ent.Owner, out InventorySlotsComponent? slots) &&
            slots.VisualLayerKeys.TryGetValue("neck", out var neckKeys))
        {
            buffer.AddRange(neckKeys);
        }

        return buffer;
    }

    /// <summary>
    ///     The tail/wings visual layers and their marking layers, restricted to keys the sprite actually has (the
    ///     "Wings" visual layer only exists for species that define it).
    /// </summary>
    private List<object> GetTailBlockKeys(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, List<object> buffer)
    {
        buffer.Clear();
        buffer.Add(HumanoidVisualLayers.Tail);
        buffer.Add(HumanoidVisualLayers.Wings);

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
                        buffer.Add(GetMarkingKey(marking.MarkingId, rsi.RsiState));
                }
            }
        }

        for (var i = buffer.Count - 1; i >= 0; i--)
        {
            if (!TryGetLayerIndex(ent, buffer[i], out _))
                buffer.RemoveAt(i);
        }

        return buffer;
    }

    /// <summary>
    ///     Interns the "markingId-rsiState" layer key. Every humanoid re-discovers its block keys on each reorder,
    ///     and both halves of the key come from prototypes, so a shared cache keeps that discovery allocation-free
    ///     after the first pass.
    /// </summary>
    private object GetMarkingKey(string markingId, string rsiState)
    {
        var key = (markingId, rsiState);
        if (_markingKeyCache.TryGetValue(key, out var cached))
            return cached;

        var composed = $"{markingId}-{rsiState}";
        _markingKeyCache[key] = composed;
        return composed;
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
            // Capture the front anchor while the block still rests in its native front-facing layout (below the
            // cluster), i.e. before the move below hoists it: the anchor is only discoverable from that position.
            // Without this a character whose first facing is Back would never record the anchor, and the next Front
            // pass would fall back to resetting the hair below the mask, which lands above the species' head-trim
            // base layer when that layer (e.g. arachnid HeadSide) sits below the mask.
            if (cache.HairFrontAnchor is null &&
                TryGetBlockExtent(ent, cache.HairKeys, out _, out var backBlockEnd) &&
                backBlockEnd <= clusterTopIdx &&
                TryGetFrontAnchor(ent, backBlockEnd, out var backAnchor))
            {
                cache.HairFrontAnchor = backAnchor;
            }

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
            !TryResolveBlock(ent, cache.TailKeys, out var tailIndices, out var tailStart, out var tailEnd) ||
            !TryResolveBlock(ent, cache.CloakKeys, out _, out _, out var cloakTop))
        {
            return;
        }

        // The native base layout has the cloak below the tail, which is also the back view's layout.
        var cloakBelowTail = cloakTop < tailStart;

        var wantCloakBelowTail = view == DirectionalView.Back;

        if (ent.Comp1.Species == HarpySpecies)
        {
            // Harpies draw big back wings on the Tail layer above the head in their base order, so the cloak (neck)
            // is already below them on every view; keep the native layout rather than burying the wings under it.
            return;
        }

        // Only the extremes of the blocks are compared here, so a tail block split by a marking-layer rebuild
        // (base still sunk on an old index, marking recreated above the legs) must not be treated as already in
        // place: fall through to the re-stitching path below.
        if (IsResolvedBlockContiguous(tailIndices, tailStart, tailEnd) &&
            cloakBelowTail == wantCloakBelowTail)
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
            // Back view: tail back on its native spot, directly above its anchor; cloak right below the tail.
            if (cache.TailAnchorCaptured && TryGetLayerIndex(ent, cache.TailAnchor!, out var anchorIdx))
            {
                var backTailTarget = anchorIdx + 1;
                InsertBlock(ent, tailBlock, backTailTarget);
                InsertBlock(ent, cloakBlock, backTailTarget - cloakCount);
                return;
            }

            // Anchor unknown (rare, e.g. the cache was rebuilt mid-front-view): fall back to the head-trim cluster.
            if (TryGetClusterTop(ent, out _, out var clusterIdx))
            {
                var backTailTarget = clusterIdx + 1;
                InsertBlock(ent, tailBlock, backTailTarget);
                InsertBlock(ent, cloakBlock, backTailTarget - cloakCount);
                return;
            }

            InsertBlock(ent, tailBlock, tailStart);
            InsertBlock(ent, cloakBlock, cloakStart);
            return;
        }

        // Front/side view: cloak spread below the head, tail tucked directly under the cloak. The cloak is left
        // above any back-slot bag, so bags no longer render over it.
        if (!TryGetLayerIndex(ent, "head", out var headIdx))
        {
            InsertBlock(ent, tailBlock, tailStart);
            InsertBlock(ent, cloakBlock, cloakStart);
            return;
        }

        var cloakTarget = headIdx - cloakCount;
        var tailTarget = Math.Max(0, cloakTarget - tailCount);
        // Cloak goes in first, directly below the head; the tail follows below it. Inserting the tail first would
        // shift the belt/outerClothing layers up onto the cloak's index, drawing them over it.
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
    ///     True when the resolved block occupies a contiguous run of indices: every index between <paramref name="start"/>
    ///     and <paramref name="end"/> is present. Duplicate keys that resolve to the same layer (e.g. the same marking
    ///     listed twice) are fine. A split block — a Tail base stuck on its old index while a tail marking was recreated
    ///     above the legs by an appearance update — reads as contiguous extremes even though a stray layer floats above
    ///     the legs, so the early exits that rely on block positions must not fire while the block is split.
    /// </summary>
    private static bool IsResolvedBlockContiguous(
        List<(object Key, int Index)> indices,
        int start,
        int end)
    {
        if (indices.Count == 0 || end < start)
            return false;

        for (var expected = start; expected <= end; expected++)
        {
            var found = false;
            for (var i = 0; i < indices.Count; i++)
            {
                if (indices[i].Index != expected)
                    continue;

                found = true;
                break;
            }

            if (!found)
                return false;
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

        // Already in the layout this view wants. Only trust the extremes when the block is also contiguous: a
        // split block's extremes can line up with the anchor while a stray layer floats elsewhere in the sprite.
        if (IsResolvedBlockContiguous(indices, blockStart, blockEnd) &&
            (placeAbove ? blockStart == anchorStart + 1 : blockEnd == anchorStart - 1))
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
