using System.Numerics;
using Content.Shared._Arcane.Leash;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._Arcane.Leash;

public sealed class LeashOverlay : Overlay
{
    private readonly IEntityManager _entityManager;
    private readonly SharedTransformSystem _transformSystem;

    private readonly Vector2[] _shadowVerts = new Vector2[4];
    private readonly Vector2[] _leashVerts = new Vector2[4];

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
            var length = dir.Length();
            if (length < 0.0001f)
                continue;

            // Перпендикулярный вектор (Для толщины)
            var perp = new Vector2(-dir.Y, dir.X) / length;

            var thickness = 0.08f; // Толщина 
            var halfThick = thickness * 0.5f;
            var shadowOffset = new Vector2(0.04f, -0.04f); // Смещение тени (право, низ)

            var p1 = startPos + perp * halfThick;
            var p2 = startPos - perp * halfThick;
            var p3 = endPos - perp * halfThick;
            var p4 = endPos + perp * halfThick;

            // Тень
            var shadowColor = Color.Black.WithAlpha(0.35f);
            _shadowVerts[0] = p1 + shadowOffset;
            _shadowVerts[1] = p2 + shadowOffset;
            _shadowVerts[2] = p4 + shadowOffset;
            _shadowVerts[3] = p3 + shadowOffset;
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _shadowVerts, shadowColor);

            // Поводок
            var leashColor = Color.SaddleBrown;
            _leashVerts[0] = p1;
            _leashVerts[1] = p2;
            _leashVerts[2] = p4;
            _leashVerts[3] = p3;
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _leashVerts, leashColor);
        }
    }
}
