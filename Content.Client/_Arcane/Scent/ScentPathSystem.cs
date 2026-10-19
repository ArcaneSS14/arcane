using Content.Shared._Arcane.Scent;
using Content.Shared._EinsteinEngines.Forensics;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Client._Arcane.Scent;

public sealed class ScentPathSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly EntProtoId Effect = "ScentTrackEffect";
    private static readonly TimeSpan FadeInterval = TimeSpan.FromSeconds(0.5);

    private readonly Dictionary<NetCoordinates, (EntityUid Effect, TimeSpan Time)> _points = new();
    private readonly List<NetCoordinates> _expired = new();
    private TimeSpan _lifetime;
    private TimeSpan _nextFade;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ScentPathMessage>(OnScentPath);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        Clear();
    }

    private void OnScentPath(ScentPathMessage ev)
    {
        if (ev.Reset)
            Clear();

        if (ev.Lifetime <= TimeSpan.Zero)
            return;

        _lifetime = ev.Lifetime;

        foreach (var point in ev.Points)
        {
            if (_points.TryGetValue(point.Coordinates, out var existing))
            {
                _points[point.Coordinates] = (existing.Effect, point.Time);
                continue;
            }

            var coords = GetCoordinates(point.Coordinates);
            if (!coords.IsValid(EntityManager))
                continue;

            var effect = Spawn(Effect, coords);
            RemComp<TimedDespawnComponent>(effect);
            _points[point.Coordinates] = (effect, point.Time);
        }

        _nextFade = TimeSpan.Zero;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_points.Count == 0)
            return;

        if (!TryComp<ScentTrackerComponent>(_player.LocalEntity, out var tracker) || tracker.Scent == string.Empty)
        {
            Clear();
            return;
        }

        var now = _timing.CurTime;
        if (now < _nextFade)
            return;

        _nextFade = now + FadeInterval;

        foreach (var (key, (effect, time)) in _points)
        {
            var left = time + _lifetime - now;
            if (left <= TimeSpan.Zero || !TryComp<SpriteComponent>(effect, out var sprite))
            {
                _expired.Add(key);
                continue;
            }

            var alpha = (float) (left / _lifetime);
            _sprite.SetColor((effect, sprite), sprite.Color.WithAlpha(0.2f + alpha * 0.8f));
        }

        foreach (var key in _expired)
        {
            QueueDel(_points[key].Effect);
            _points.Remove(key);
        }

        _expired.Clear();
    }

    private void Clear()
    {
        foreach (var (effect, _) in _points.Values)
        {
            QueueDel(effect);
        }

        _points.Clear();
    }
}
