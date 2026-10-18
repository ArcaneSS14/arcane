using Content.Server._EinsteinEngines.Forensics.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Arcane.Scent;
using Content.Shared._EinsteinEngines.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.Scent;

public sealed class ScentPathSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private static readonly TimeSpan RecordInterval = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CarryCheckInterval = TimeSpan.FromSeconds(5);
    private const int MaxGap = 3;

    private TimeSpan _nextRecord;
    private TimeSpan _nextSend;
    private TimeSpan _nextCarryCheck;

    private readonly Dictionary<string, ScentPathComponent> _trailsByScent = new();
    private readonly List<(EntityUid Grid, Vector2i Tile)> _expired = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScentComponent, DidEquipEvent>(OnEquip);
        SubscribeLocalEvent<ScentComponent, DidEquipHandEvent>(OnEquipHand);
        SubscribeLocalEvent<ScentPathViewerComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        if (now >= _nextRecord)
        {
            _nextRecord = now + RecordInterval;
            Record(now);
        }

        if (now >= _nextSend)
        {
            _nextSend = now + SendInterval;
            Send(now);
        }

        if (now >= _nextCarryCheck)
        {
            _nextCarryCheck = now + CarryCheckInterval;
            CheckCarry(now);
        }
    }

    private void Record(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ScentPathComponent, ScentComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var trail, out var scent, out var xform))
        {
            if (trail.Scent != scent.Scent)
            {
                trail.Points.Clear();
                trail.Order.Clear();
                trail.Scent = scent.Scent;
                trail.LastGrid = null;
            }

            while (trail.Order.TryPeek(out var entry) && entry.Time + trail.Lifetime <= now)
            {
                trail.Order.Dequeue();

                if (trail.Points.TryGetValue(entry.Key, out var point) && point.Time == entry.Time)
                    trail.Points.Remove(entry.Key);
            }

            if (trail.Lifetime <= TimeSpan.Zero
                || trail.Scent == string.Empty
                || _container.IsEntityInContainer(uid)
                || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
            {
                trail.LastGrid = null;
                continue;
            }

            var gridUid = xform.GridUid.Value;
            var tile = _map.TileIndicesFor(gridUid, grid, _transform.GetMoverCoordinates(uid, xform));

            if (trail.LastGrid == gridUid && trail.LastTile == tile)
                continue;

            var from = trail.LastGrid == gridUid ? trail.LastTile : tile;
            var delta = tile - from;
            var steps = Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));

            // teleport or throw
            if (steps > MaxGap)
                steps = 0;

            trail.LastGrid = gridUid;
            trail.LastTile = tile;

            for (var i = 1; i <= steps; i++)
            {
                var step = new Vector2i(
                    from.X + (int) MathF.Round(delta.X * i / (float) steps),
                    from.Y + (int) MathF.Round(delta.Y * i / (float) steps));

                AddPoint(trail, (gridUid, grid), xform.MapUid, step, now);
            }

            if (steps == 0)
                AddPoint(trail, (gridUid, grid), xform.MapUid, tile, now);
        }
    }

    private void AddPoint(ScentPathComponent trail, Entity<MapGridComponent> grid, EntityUid? map, Vector2i tile, TimeSpan now)
    {
        var mixture = _atmos.GetTileMixture(grid.Owner, map, tile);
        if (mixture == null || mixture.Pressure < trail.MinPressure)
            return;

        var key = (grid.Owner, tile);
        trail.Points[key] = (_map.GridTileToLocal(grid, grid, tile), now);
        trail.Order.Enqueue((key, now));
    }

    private void Send(TimeSpan now)
    {
        _trailsByScent.Clear();
        var collected = false;

        var trackers = EntityQueryEnumerator<ScentTrackerComponent, ActorComponent, TransformComponent>();
        while (trackers.MoveNext(out var uid, out var tracker, out var actor, out var xform))
        {
            if (tracker.Scent == string.Empty)
            {
                RemCompDeferred<ScentPathViewerComponent>(uid);
                continue;
            }

            if (!collected)
            {
                collected = true;

                var trails = EntityQueryEnumerator<ScentPathComponent>();
                while (trails.MoveNext(out var trail))
                {
                    if (trail.Scent != string.Empty)
                        _trailsByScent[trail.Scent] = trail;
                }
            }

            var viewer = EnsureComp<ScentPathViewerComponent>(uid);
            var reset = viewer.Scent != tracker.Scent;
            if (reset)
            {
                viewer.Scent = tracker.Scent;
                viewer.Sent.Clear();
            }

            if (!_trailsByScent.TryGetValue(tracker.Scent, out var path))
            {
                viewer.Sent.Clear();
                if (reset)
                    RaiseNetworkEvent(new ScentPathMessage(true, TimeSpan.Zero, new()), actor.PlayerSession);

                continue;
            }

            foreach (var (key, time) in viewer.Sent)
            {
                if (time + path.Lifetime <= now)
                    _expired.Add(key);
            }

            foreach (var key in _expired)
            {
                viewer.Sent.Remove(key);
            }

            _expired.Clear();

            var origin = _transform.GetMapCoordinates(uid, xform);
            var rangeSquared = tracker.PathRange * tracker.PathRange;
            var points = new List<ScentPathPoint>();

            foreach (var (key, (coords, time)) in path.Points)
            {
                if (viewer.Sent.TryGetValue(key, out var sent) && sent == time
                    || !coords.IsValid(EntityManager))
                    continue;

                var map = _transform.ToMapCoordinates(coords, false);
                if (map.MapId != origin.MapId || (map.Position - origin.Position).LengthSquared() > rangeSquared)
                    continue;

                viewer.Sent[key] = time;
                points.Add(new ScentPathPoint(GetNetCoordinates(coords), time));
            }

            if (reset || points.Count > 0)
                RaiseNetworkEvent(new ScentPathMessage(reset, path.Lifetime, points), actor.PlayerSession);
        }
    }

    private void OnPlayerAttached(EntityUid uid, ScentPathViewerComponent comp, PlayerAttachedEvent args)
    {
        RemCompDeferred<ScentPathViewerComponent>(uid);
    }

    private void OnEquip(EntityUid uid, ScentComponent comp, DidEquipEvent args)
    {
        StartCarry(uid, args.Equipment);
    }

    private void OnEquipHand(EntityUid uid, ScentComponent comp, DidEquipHandEvent args)
    {
        StartCarry(uid, args.Equipped);
    }

    private void StartCarry(EntityUid holder, EntityUid item)
    {
        if (HasComp<ScentComponent>(item))
            return;

        var carry = EnsureComp<ScentCarryComponent>(item);
        if (carry.Holder == holder)
            return;

        carry.Holder = holder;
        carry.Since = _timing.CurTime;
    }

    private void CheckCarry(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ScentCarryComponent>();
        while (query.MoveNext(out var uid, out var carry))
        {
            if (!_container.TryGetContainingContainer(uid, out var container)
                || container.Owner != carry.Holder
                || !TryComp<ScentPathComponent>(carry.Holder, out var path)
                || !TryComp<ScentComponent>(carry.Holder, out var scent))
            {
                RemCompDeferred<ScentCarryComponent>(uid);
                continue;
            }

            if (now - carry.Since < path.CarryTime)
                continue;

            AddScent(uid, scent.Scent);
            RemCompDeferred<ScentCarryComponent>(uid);
        }
    }

    public void AddScent(EntityUid uid, string scent)
    {
        if (scent == string.Empty)
            return;

        var forensics = EnsureComp<ForensicsComponent>(uid);
        if (forensics.MaxScents <= 0)
            return;

        forensics.Scents.Remove(scent);
        forensics.Scents.Add(scent);

        while (forensics.Scents.Count > forensics.MaxScents)
            forensics.Scents.RemoveAt(0);

        Dirty(uid, forensics);
    }
}
