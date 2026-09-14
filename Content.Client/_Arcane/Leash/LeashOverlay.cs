using Content.Shared._Arcane.Leash;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Content.Client._Arcane.Leash;

public sealed class LeashOverlay : Overlay
{
    private readonly IEntityManager _entityManager;
    private readonly SharedTransformSystem _transformSystem;

    /// <summary>
    /// Rendering buffers
    /// </summary>
    private readonly List<Vector2> _shadowVerts = new();
    private readonly List<Vector2> _leashVerts = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public LeashOverlay(IEntityManager entityManager)
    {
        _entityManager = entityManager;
        _transformSystem = _entityManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entityManager.EntityQueryEnumerator<LeashComponent, TransformComponent>();

        _shadowVerts.Clear();
        _leashVerts.Clear();

        while (query.MoveNext(out var uid, out var leash, out var leashXform))
        {
            if (leash.AttachedEntity is not { } target)
                continue;

            if (!_entityManager.TryGetComponent<TransformComponent>(target, out var targetXform))
                continue;

            if (leashXform.MapID != targetXform.MapID)
                continue;

            var startPos = _transformSystem.GetWorldPosition(leashXform);
            var endPos = _transformSystem.GetWorldPosition(targetXform);

            var dir = endPos - startPos;
            var lengthSq = dir.LengthSquared();

            // If the length isn’t 0, then we continue
            if (lengthSq < 0.000001f)
                continue;

            var length = MathF.Sqrt(lengthSq);
            var perp = new Vector2(-dir.Y, dir.X) / length;

            var thickness = 0.08f;
            var halfThick = thickness * 0.5f;
            var shadowOffset = new Vector2(0.04f, -0.04f);

            var p1 = startPos + perp * halfThick;
            var p2 = startPos - perp * halfThick;
            var p3 = endPos - perp * halfThick;
            var p4 = endPos + perp * halfThick;

            // Creating 2 triangles for shadows
            var sp1 = p1 + shadowOffset;
            var sp2 = p2 + shadowOffset;
            var sp3 = p3 + shadowOffset;
            var sp4 = p4 + shadowOffset;

            _shadowVerts.Add(sp1); _shadowVerts.Add(sp2); _shadowVerts.Add(sp4);
            _shadowVerts.Add(sp2); _shadowVerts.Add(sp3); _shadowVerts.Add(sp4);

            // We’re making 2 triangles for the leash
            _leashVerts.Add(p1); _leashVerts.Add(p2); _leashVerts.Add(p4);
            _leashVerts.Add(p2); _leashVerts.Add(p3); _leashVerts.Add(p4);
        }

        // Shadows
        if (_shadowVerts.Count > 0)
        {
            handle.DrawPrimitives(
                DrawPrimitiveTopology.TriangleList,
                CollectionsMarshal.AsSpan(_shadowVerts),
                Color.Black.WithAlpha(0.35f));
        }

        // Leashes
        if (_leashVerts.Count > 0)
        {
            handle.DrawPrimitives(
                DrawPrimitiveTopology.TriangleList,
                CollectionsMarshal.AsSpan(_leashVerts),
                Color.SaddleBrown);
        }
    }
}
