using System.Numerics;
using Content.Client.Stylesheets.Colorspace;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Arcane.Humanoid;

public sealed class HairGradientSystem : EntitySystem
{
    public static readonly ProtoId<ShaderPrototype> ShaderPrototype = "ArcaneHairGradient";

    private static readonly Vector4 NoFrame = new(-1f);

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, LayerShaders> _shaders = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        foreach (var (uid, layers) in _shaders)
        {
            foreach (var (layerId, shader) in layers.ByLayer)
                DisposeLayerShader(uid, layerId, shader);
        }

        _shaders.Clear();
    }

    private void OnShutdown(Entity<HumanoidAppearanceComponent> ent, ref ComponentShutdown args)
    {
        if (!_shaders.Remove(ent, out var layers))
            return;

        foreach (var (layerId, shader) in layers.ByLayer)
            DisposeLayerShader(ent, layerId, shader);
    }

    public ShaderInstance GetLayerShader(Entity<HumanoidAppearanceComponent> ent, string layerId, SpriteSpecifier.Rsi rsi)
    {
        var layers = _shaders.GetOrNew(ent);
        layers.Applied.Add(layerId);
        if (!layers.ByLayer.TryGetValue(layerId, out var shader))
        {
            shader = _prototype.Index(ShaderPrototype).InstanceUnique();
            layers.ByLayer[layerId] = shader;
        }

        var colors = ent.Comp.HairGradientColors;
        var roots = colors.Count > 0 ? colors[0] : Color.Black;
        var tips = colors.Count > 1 ? colors[1] : roots;

        shader.SetParameter("rootsLab", ToOklab(roots));
        shader.SetParameter("tipsLab", ToOklab(tips));
        shader.SetParameter("gradientStyle", (int) ent.Comp.HairGradientStyle);
        shader.SetParameter("gradientOffset", ent.Comp.HairGradientOffset);

        // The shader cannot tell which direction is drawn, so it compares UV against each direction's atlas frame.
        var state = _sprite.GetState(rsi);
        shader.SetParameter("frameWidth", (float) state.Size.X);
        shader.SetParameter("frameNorth", GetFrameUv(state, RsiDirection.North));
        shader.SetParameter("frameEast", GetFrameUv(state, RsiDirection.East));
        shader.SetParameter("frameWest", GetFrameUv(state, RsiDirection.West));

        return shader;
    }

    // Marking layers are rebuilt on every sprite update, so a shader is kept until a rebuild stops applying it.
    public void ReleaseUnusedShaders(EntityUid uid)
    {
        if (!_shaders.TryGetValue(uid, out var layers))
            return;

        foreach (var (layerId, shader) in layers.ByLayer)
        {
            if (layers.Applied.Contains(layerId))
                continue;

            layers.ByLayer.Remove(layerId);
            DisposeLayerShader(uid, layerId, shader);
        }

        layers.Applied.Clear();
        if (layers.ByLayer.Count == 0)
            _shaders.Remove(uid);
    }

    private void DisposeLayerShader(EntityUid uid, string layerId, ShaderInstance shader)
    {
        // Detach first. A renderer may still hold the SpriteLayer after the humanoid component is shut down.
        if (TryComp<SpriteComponent>(uid, out var sprite) &&
            _sprite.LayerMapTryGet((uid, sprite), layerId, out var index, false) &&
            sprite[index] is SpriteComponent.Layer layer &&
            ReferenceEquals(layer.Shader, shader))
        {
            sprite.LayerSetShader(index, (ShaderInstance?) null);
        }

        shader.Dispose();
    }

    private static Vector3 ToOklab(Color color)
    {
        var lab = color.LabFromSrgb();
        return new Vector3(lab.X, lab.Y, lab.Z);
    }

    private static Vector4 GetFrameUv(RSI.State state, RsiDirection direction)
    {
        if (state.RsiDirections == RsiDirectionType.Dir1 || state.GetFrame(direction, 0) is not AtlasTexture frame)
            return NoFrame;

        var (width, height) = frame.SourceTexture.Size;
        var region = frame.SubRegion;
        return new Vector4(
            region.Left / width,
            (height - region.Bottom) / height,
            region.Right / width,
            (height - region.Top) / height);
    }

    private sealed class LayerShaders
    {
        public readonly Dictionary<string, ShaderInstance> ByLayer = new();
        public readonly HashSet<string> Applied = new();
    }
}
