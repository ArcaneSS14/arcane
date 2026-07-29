// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Explosion.Components;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Explosion;

[UsedImplicitly]
public sealed class ExplosionOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

//    [Dependency] private readonly IRobustRandom _robustRandom = default!; // Orion-Edit: Removed

    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    private readonly SharedTransformSystem _transformSystem;
    private SharedAppearanceSystem _appearance;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private ShaderInstance _shader;

    // Orion-Start
    private const int FireStates = 3;
    private const string FireRsiPath = "/Textures/_Orion/Effects/tile_fire_explode.rsi";
    private readonly float[] _fireTimer = new float[FireStates];
    private readonly float[][] _fireFrameDelays = new float[FireStates][];
    private readonly int[] _fireFrameCounter = new int[FireStates];
    private readonly Texture[][] _fireFrames = new Texture[FireStates][];
    // Orion-End

    public ExplosionOverlay(SharedAppearanceSystem appearanceSystem, IResourceCache resourceCache) // Orion-Edit
    {
        IoCManager.InjectDependencies(this);
        _shader = _proto.Index(UnshadedShader).Instance();
        _transformSystem = _entMan.System<SharedTransformSystem>();
        _appearance = appearanceSystem;
        // Orion-Start
        var fire = resourceCache.GetResource<RSIResource>(FireRsiPath).RSI;

        for (var i = 0; i < FireStates; i++)
        {
            if (!fire.TryGetState((i + 1).ToString(), out var state))
                throw new ArgumentOutOfRangeException($"Fire RSI doesn't have state \"{i}\"!");

            _fireFrames[i] = state.GetFrames(RsiDirection.South);
            _fireFrameDelays[i] = state.GetDelays();
            _fireFrameCounter[i] = 0;
        }
        // Orion-End
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var drawHandle = args.WorldHandle;
        drawHandle.UseShader(_shader);

        var xforms = _entMan.GetEntityQuery<TransformComponent>();
        var query = _entMan.EntityQueryEnumerator<ExplosionVisualsComponent, ExplosionVisualsTexturesComponent>();

        while (query.MoveNext(out var uid, out var visuals, out var textures))
        {
            if (visuals.Epicenter.MapId != args.MapId)
                continue;

            if (!_appearance.TryGetData(uid, ExplosionAppearanceData.Progress, out int index))
                continue;

            index = Math.Min(index, visuals.Intensity.Count - 1);
            DrawExplosion(drawHandle, args.WorldBounds, visuals, index, xforms, textures);
        }

        drawHandle.SetTransform(Matrix3x2.Identity);
        drawHandle.UseShader(null);
    }

    private void DrawExplosion(
        DrawingHandleWorld drawHandle,
        Box2Rotated worldBounds,
        ExplosionVisualsComponent visuals,
        int index,
        EntityQuery<TransformComponent> xforms,
        ExplosionVisualsTexturesComponent textures)
    {
        Box2 gridBounds;
        foreach (var (gridId, tiles) in visuals.Tiles)
        {
            if (!_entMan.TryGetComponent(gridId, out MapGridComponent? grid))
                continue;

            var xform = xforms.GetComponent(gridId);
            var (_, _, worldMatrix, invWorldMatrix) = _transformSystem.GetWorldPositionRotationMatrixWithInv(xform, xforms);

            gridBounds = invWorldMatrix.TransformBox(worldBounds).Enlarged(grid.TileSize * 2);
            drawHandle.SetTransform(worldMatrix);

            DrawTiles(drawHandle, gridBounds, index, tiles, visuals, grid.TileSize, textures);
        }

        if (visuals.SpaceTiles == null)
            return;

        Matrix3x2.Invert(visuals.SpaceMatrix, out var invSpace);
        gridBounds = invSpace.TransformBox(worldBounds).Enlarged(2);
        drawHandle.SetTransform(visuals.SpaceMatrix);

        DrawTiles(drawHandle, gridBounds, index, visuals.SpaceTiles, visuals, visuals.SpaceTileSize, textures);
    }

    // Orion-Start
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        for (var i = 0; i < FireStates; i++)
        {
            var delays = _fireFrameDelays[i];
            if (delays.Length == 0)
                continue;

            var frameCount = _fireFrameCounter[i];
            _fireTimer[i] += args.DeltaSeconds;
            var time = delays[frameCount];

            if (_fireTimer[i] < time) continue;
            _fireTimer[i] -= time;
            _fireFrameCounter[i] = (frameCount + 1) % _fireFrames[i].Length;
        }
    }
    // Orion-End

    private void DrawTiles(
        DrawingHandleWorld drawHandle,
        Box2 gridBounds,
        int index,
        Dictionary<int, List<Vector2i>> tileSets,
        ExplosionVisualsComponent visuals,
        ushort tileSize,
        ExplosionVisualsTexturesComponent textures)
    {
        for (var j = 0; j <= index; j++)
        {
            if (!tileSets.TryGetValue(j, out var tiles))
                continue;

/* // Orion-Edit: Removed
            var frameIndex = (int) Math.Min(visuals.Intensity[j] / textures.IntensityPerState, textures.FireFrames.Count - 1);
            var frames = textures.FireFrames[frameIndex];
*/

            foreach (var tile in tiles)
            {
                var centre = (tile + Vector2Helpers.Half) * tileSize;

                if (!gridBounds.Contains(centre))
                    continue;

                // Orion-Start
                var fireState = (int) Math.Min(visuals.Intensity[j] / textures.IntensityPerState, textures.FireFrames.Count - 1);
                var texture = _fireFrames[fireState][_fireFrameCounter[fireState]];
                // Orion-End
                drawHandle.DrawTextureRect(texture, Box2.CenteredAround(centre, new Vector2(tileSize, tileSize)), textures.FireColor);
            }
        }
    }
}
