using Content.Shared._Arcane.ERP;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Arcane.ERP;

public sealed class ArousalOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "ArousalScreenEffect";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private const float FadeSpeed = 3.5f;
    private const float MinimumVisibleIntensity = 0.005f;

    private readonly ArousalSystem _arousal;
    private readonly ShaderInstance _shader;
    private float _currentIntensity;

    public float MotionScale { get; set; } = 1f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public ArousalOverlay()
    {
        IoCManager.InjectDependencies(this);

        _arousal = _entityManager.System<ArousalSystem>();
        _shader = _prototypeManager.Index(Shader).InstanceUnique();
        var heartTexture = _resourceCache
            .GetResource<TextureResource>("/Textures/_Arcane/Interface/heartIcon.png")
            .Texture;
        _shader.SetParameter("heartTexture", heartTexture);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var targetIntensity = 0f;
        if (_playerManager.LocalEntity is { Valid: true } player &&
            _entityManager.TryGetComponent(player, out ArousalComponent? arousal) &&
            arousal.MaxArousal > 0f)
        {
            var normalizedArousal = Math.Clamp(_arousal.GetArousal(arousal) / arousal.MaxArousal, 0f, 1f);
            targetIntensity = MathF.Pow(normalizedArousal, 0.7f);
        }

        // Сглаживание редких ступенчатых обновлений сетевого значения.
        var interpolation = Math.Clamp(args.DeltaSeconds * FadeSpeed, 0f, 1f);
        _currentIntensity = MathHelper.Lerp(_currentIntensity, targetIntensity, interpolation);
        if (MathHelper.CloseTo(_currentIntensity, targetIntensity, 0.001f))
            _currentIntensity = targetIntensity;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_currentIntensity <= MinimumVisibleIntensity ||
            _playerManager.LocalEntity is not { Valid: true } player ||
            !_entityManager.TryGetComponent(player, out EyeComponent? eye))
        {
            return false;
        }

        return args.Viewport.Eye == eye.Eye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _shader.SetParameter("intensity", _currentIntensity);
        _shader.SetParameter("motionScale", MotionScale);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
