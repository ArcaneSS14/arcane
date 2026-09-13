using System.Collections.Generic;
using Content.Client.Inventory;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Arcane.DirectionalLayering;

/// <summary>
///     Reorders a humanoid sprite's layers based on the direction the entity is facing.
/// </summary>
public sealed class DirectionalLayeringSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private Angle _lastEyeRotation = Angle.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, MoveEvent>(OnMove);
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

    private void OnMove(EntityUid uid, HumanoidAppearanceComponent component, ref MoveEvent args)
    {
        if (args.OldRotation.GetCardinalDir() == args.NewRotation.GetCardinalDir())
            return;

        if (TryComp(uid, out SpriteComponent? sprite))
            ApplyOrdering((uid, component, sprite));
    }

    private void ApplyOrdering(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        if (!TryComp(ent.Owner, out TransformComponent? xform))
            return;

        // The renderer picks the RSI direction from `worldRotation + eyeRotation`, so the "back view" has to be
        // determined relative to the camera, not the absolute world rotation.
        var backView = IsBackView(_transform.GetWorldRotation(ent.Owner) + _eyeManager.CurrentEye.Rotation);
        var candidateKeys = GetCandidateLayerKeys(ent);

        UpdateHairEars(ent, backView, candidateKeys);
        UpdateNeckTail(ent, backView, candidateKeys);
    }

    /// <summary>
    ///     Every layer map key that can live inside one of the reordered blocks.
    /// </summary>
    private List<object> GetCandidateLayerKeys(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        var keys = new List<object>
        {
            HumanoidVisualLayers.Hair,
            HumanoidVisualLayers.HeadTop,
            HumanoidVisualLayers.Tail,
            HumanoidVisualLayers.Wings,
            HumanoidVisualLayers.SnoutCover,
            "neck",
            "mask",
            "head",
        };

        foreach (var markingList in ent.Comp1.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (!_markingManager.TryGetMarking(marking, out var prototype))
                    continue;

                foreach (var sprite in prototype.Sprites)
                {
                    if (sprite is SpriteSpecifier.Rsi rsi)
                        keys.Add($"{marking.MarkingId}-{rsi.RsiState}");
                }
            }
        }

        if (TryComp(ent.Owner, out InventorySlotsComponent? slots) &&
            slots.VisualLayerKeys.TryGetValue("neck", out var neckKeys))
        {
            keys.AddRange(neckKeys);
        }

        return keys;
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

    private void UpdateHairEars(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, bool backView, List<object> candidateKeys)
    {
        if (!TryGetLayerIndex(ent, HumanoidVisualLayers.Hair, out var hairIdx) ||
            !TryGetLayerIndex(ent, HumanoidVisualLayers.HeadTop, out var headTopIdx) ||
            !TryGetLayerIndex(ent, "mask", out var maskIdx) ||
            !TryGetLowerBoundary(ent, out var boundaryIdx))
        {
            return;
        }

        var hairAboveEars = hairIdx > headTopIdx;
        if (hairAboveEars == backView)
            return;

        if (backView)
        {
            // Hair currently below the mask (front layout): move the hair block directly above the ears.
            var count = maskIdx - hairIdx;
            MoveLayerBlock(ent, candidateKeys, hairIdx, count, boundaryIdx - count);
        }
        else
        {
            // Hair currently above the ears (back layout): move the hair block back below the mask.
            var count = boundaryIdx - hairIdx;
            MoveLayerBlock(ent, candidateKeys, hairIdx, count, maskIdx);
        }
    }

    private void UpdateNeckTail(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, bool backView, List<object> candidateKeys)
    {
        if (!TryGetLayerIndex(ent, "neck", out var neckIdx) ||
            !TryGetLayerIndex(ent, "head", out var headIdx) ||
            !TryGetLayerIndex(ent, HumanoidVisualLayers.SnoutCover, out var snoutCoverIdx) ||
            !TryGetLayerIndex(ent, HumanoidVisualLayers.Tail, out var tailIdx))
        {
            return;
        }

        var neckAboveTail = neckIdx > tailIdx;
        if (neckAboveTail == !backView)
            return;

        if (backView)
        {
            // Cloak currently above the tail/wings (front layout): move the neck block back below the snout cover.
            var count = headIdx - neckIdx;
            MoveLayerBlock(ent, candidateKeys, neckIdx, count, snoutCoverIdx);
        }
        else
        {
            // Cloak currently below the tail/wings (back layout): move the neck block above them.
            var count = snoutCoverIdx - neckIdx;
            MoveLayerBlock(ent, candidateKeys, neckIdx, count, headIdx - count);
        }
    }

    /// <summary>
    ///     Index of the first layer that sits right above the ears block: either the tail, the wings or the head.
    /// </summary>
    private bool TryGetLowerBoundary(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, out int index)
    {
        index = int.MaxValue;
        var found = false;

        if (TryGetLayerIndex(ent, HumanoidVisualLayers.Tail, out var tailIdx))
        {
            index = Math.Min(index, tailIdx);
            found = true;
        }

        if (TryGetLayerIndex(ent, HumanoidVisualLayers.Wings, out var wingsIdx))
        {
            index = Math.Min(index, wingsIdx);
            found = true;
        }

        if (TryGetLayerIndex(ent, "head", out var headIdx))
        {
            index = Math.Min(index, headIdx);
            found = true;
        }

        return found;
    }

    /// <summary>
    ///     Mirrors <see cref="Layer.GetDirection"/> for 4-directional layers (what humanoid body parts use), including
    ///     the anti-flicker direction bias, so this system's ordering matches the sprite states that actually render.
    /// </summary>
    private static bool IsBackView(Angle angle)
    {
        var ang = angle.Reduced().FlipPositive().Theta;
        var mod = (Math.Floor(ang / MathHelper.PiOver2) % 2) - 0.5;
        var modTheta = ang + mod * DirectionBias;
        return (int) Math.Round(modTheta / MathHelper.PiOver2) % 4 == 2;
    }

    /// <summary>
    ///     Removes a contiguous block of layers and inserts it elsewhere, keeping their relative order and re-registering
    ///     every layer map key that pointed into the block. The block ends up immediately before the layer that currently
    ///     sits at <paramref name="insertBeforeIndex"/>.
    /// </summary>
    private void MoveLayerBlock(
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        List<object> candidateKeys,
        int start,
        int count,
        int insertBeforeIndex)
    {
        if (count <= 0)
            return;

        var sprite = ent.Comp2;
        var captured = new List<(object Key, int Relative)>();

        foreach (var key in candidateKeys)
        {
            if (!TryGetLayerIndex(ent, key, out var index) || index < start || index >= start + count)
                continue;

            captured.Add((key, index - start));
        }

        var block = new Layer[count];
        for (var i = count - 1; i >= 0; i--)
        {
            _sprite.RemoveLayer((ent.Owner, sprite), start + i, out var layer, false);
            block[i] = layer!;
        }

        for (var i = 0; i < count; i++)
            _sprite.AddLayer((ent.Owner, sprite), block[i], insertBeforeIndex + i);

        foreach (var (key, relative) in captured)
            SetLayerIndex(ent, key, insertBeforeIndex + relative);
    }
}
