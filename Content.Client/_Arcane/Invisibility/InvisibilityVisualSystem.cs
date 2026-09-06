using Content.Shared._Arcane.Invisibility;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Arcane.Invisibility;

/// <summary>
///     Applies the arcane invisibility shader only to the humanoid's body sprite layers (skin,
///     hair, markings). Clothing layers never receive the shader, so an invisible arcane person
///     looks like a translucent/ghostly body wearing perfectly solid, opaque clothes.
/// </summary>
public sealed class InvisibilityVisualSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> ShaderPrototype = "ArcaneInvisibility";

    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly MarkingManager _marking = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArcaneInvisibilityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ArcaneInvisibilityComponent, AfterAutoHandleStateEvent>(OnArcaneState);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        foreach (var shader in _shaders.Values)
            shader.Dispose();
        _shaders.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ArcaneInvisibilityComponent, HumanoidAppearanceComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var arcane, out _, out _))
            ApplyShader(uid, arcane);
    }

    private void OnArcaneState(Entity<ArcaneInvisibilityComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplyShader(ent.Owner, ent.Comp);
    }

    private void OnShutdown(Entity<ArcaneInvisibilityComponent> ent, ref ComponentShutdown args)
    {
        RemoveShader(ent.Owner);
    }

    private void ApplyShader(EntityUid uid, ArcaneInvisibilityComponent arcane)
    {
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid) ||
            !TryComp<SpriteComponent>(uid, out var sprite))
        {
            return;
        }

        var shader = GetShader(uid, arcane.ShaderVisibility);

        foreach (var layer in humanoid.BaseLayers.Keys)
        {
            if (_sprite.LayerMapTryGet((uid, sprite), layer, out var index, false))
                sprite.LayerSetShader(index, shader);
        }

        foreach (var markingList in humanoid.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (!_marking.TryGetMarking(marking, out var prototype))
                    continue;

                foreach (var spriteSpec in prototype.Sprites)
                {
                    if (spriteSpec is not SpriteSpecifier.Rsi rsi)
                        continue;

                    var layerId = $"{marking.MarkingId}-{rsi.RsiState}";
                    if (_sprite.LayerMapTryGet((uid, sprite), layerId, out var markIndex, false))
                        sprite.LayerSetShader(markIndex, shader);
                }
            }
        }
    }

    private void RemoveShader(EntityUid uid)
    {
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid) ||
            !TryComp<SpriteComponent>(uid, out var sprite))
        {
            return;
        }

        foreach (var layer in humanoid.BaseLayers.Keys)
        {
            if (_sprite.LayerMapTryGet((uid, sprite), layer, out var index, false))
                sprite.LayerSetShader(index, (ShaderInstance?) null);
        }

        foreach (var markingList in humanoid.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (!_marking.TryGetMarking(marking, out var prototype))
                    continue;

                foreach (var spriteSpec in prototype.Sprites)
                {
                    if (spriteSpec is not SpriteSpecifier.Rsi rsi)
                        continue;

                    var layerId = $"{marking.MarkingId}-{rsi.RsiState}";
                    if (_sprite.LayerMapTryGet((uid, sprite), layerId, out var markIndex, false))
                        sprite.LayerSetShader(markIndex, (ShaderInstance?) null);
                }
            }
        }
    }

    private ShaderInstance GetShader(EntityUid uid, float visibility)
    {
        if (_shaders.TryGetValue(uid, out var shader))
        {
            shader.SetParameter("visibility", visibility);
            return shader;
        }

        shader = _protoMan.Index(ShaderPrototype).InstanceUnique();
        shader.SetParameter("visibility", visibility);
        _shaders[uid] = shader;
        return shader;
    }
}
