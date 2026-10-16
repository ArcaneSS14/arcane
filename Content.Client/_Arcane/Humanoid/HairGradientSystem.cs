using System.Numerics;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Arcane.Humanoid;

/// <summary>
/// Owns the per-layer hair gradient shader instances of humanoids.
/// </summary>
public sealed class HairGradientSystem : EntitySystem
{
    public static readonly ProtoId<ShaderPrototype> ShaderPrototype = "ArcaneHairGradient";

    private static readonly Vector4 NoFrame = new(-1f);

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, Dictionary<string, ShaderInstance>> _shaders = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        foreach (var shaders in _shaders.Values)
        {
            foreach (var shader in shaders.Values)
                shader.Dispose();
        }

        _shaders.Clear();
    }

    private void OnShutdown(Entity<HumanoidAppearanceComponent> ent, ref ComponentShutdown args)
    {
        if (!_shaders.Remove(ent, out var shaders))
            return;

        foreach (var shader in shaders.Values)
            shader.Dispose();
    }

    public ShaderInstance GetLayerShader(Entity<HumanoidAppearanceComponent> ent, string layerId, SpriteSpecifier.Rsi rsi)
    {
        var shaders = _shaders.GetOrNew(ent);
        if (!shaders.TryGetValue(layerId, out var shader))
        {
            shader = _prototype.Index(ShaderPrototype).InstanceUnique();
            shaders[layerId] = shader;
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

    public void ReleaseLayerShader(EntityUid uid, string layerId)
    {
        if (_shaders.TryGetValue(uid, out var shaders) && shaders.Remove(layerId, out var shader))
            shader.Dispose();
    }

    private static Vector3 ToOklab(Color color)
    {
        var lab = Color.ToLab(Color.FromSrgb(color));
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
}
