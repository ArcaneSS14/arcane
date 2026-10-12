using Content.Shared._Arcane.Scent;
using Content.Shared._EinsteinEngines.Forensics;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Arcane.Scent;

public sealed class ScentPathSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly EntProtoId Effect = "ScentTrackEffect";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ScentPathMessage>(OnScentPath);
    }

    private void OnScentPath(ScentPathMessage ev)
    {
        if (!TryComp<ScentTrackerComponent>(_player.LocalEntity, out var tracker) || tracker.Scent == string.Empty)
            return;

        var now = _timing.CurTime;

        foreach (var point in ev.Points)
        {
            if (now >= point.Expire)
                continue;

            var coords = GetCoordinates(point.Coordinates);
            if (!coords.IsValid(EntityManager))
                continue;

            var effect = Spawn(Effect, coords);
            if (!TryComp<SpriteComponent>(effect, out var sprite))
                continue;

            var alpha = (float) ((point.Expire - now) / (point.Expire - point.Time));
            _sprite.SetColor((effect, sprite), sprite.Color.WithAlpha(0.2f + alpha * 0.8f));
        }
    }
}
