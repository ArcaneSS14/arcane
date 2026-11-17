using System.Numerics;
using Content.Shared._Orion.Lighting.Shaders;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Orion.Lighting.Shaders;

//
// License-Identifier: AGPL-3.0-or-later
//

public sealed class LightingOverlay : Overlay
{
    private readonly EntityManager _entityManager;
    private readonly SpriteSystem _spriteSystem;
    private readonly TransformSystem _transformSystem;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    /// <summary>
    /// When false the overlay skips <see cref="BeforeDraw"/>, so the engine does not copy the screen for it either.
    /// </summary>
    public bool EnableGlowing;

    public LightingOverlay(EntityManager entityManager, IPrototypeManager prototypeManager)
    {
        _entityManager = entityManager;
        _spriteSystem = entityManager.EntitySysManager.GetEntitySystem<SpriteSystem>();
        _transformSystem = entityManager.EntitySysManager.GetEntitySystem<TransformSystem>();

        IoCManager.InjectDependencies(this);

        _shader = prototypeManager.Index<ShaderPrototype>("LightingOverlay").InstanceUnique();
        ZIndex = (int) DrawDepth.Effects;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return EnableGlowing;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var xformCompQuery = _entityManager.GetEntityQuery<TransformComponent>();
        var handle = args.WorldHandle;
        var bounds = args.WorldAABB.Enlarged(5f);

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        handle.UseShader(_shader);

        var query = _entityManager.AllEntityQueryEnumerator<LightingOverlayComponent, PointLightComponent, TransformComponent>();
        while (query.MoveNext(out _, out var component, out var pointLight, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (component.Enabled is false || !pointLight.Enabled)
                continue;

            var worldPos = _transformSystem.GetWorldPosition(xform, xformCompQuery);

            if (!bounds.Contains(worldPos))
                continue;

            var color = component.Color ?? pointLight.Color;
            var worldMatrix = _transformSystem.GetWorldMatrix(xform, xformCompQuery);
            handle.SetTransform(worldMatrix);

            var mask = _spriteSystem.Frame0(component.Sprite); // Mask
            var xOffset = component.OffsetX - (mask.Width / 2) / EyeManager.PixelsPerMeter;
            var yOffset = component.OffsetY - (mask.Height / 2) / EyeManager.PixelsPerMeter;
            var textureVector = new Vector2(xOffset, yOffset);

            handle.DrawTexture(mask, textureVector, color);
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
