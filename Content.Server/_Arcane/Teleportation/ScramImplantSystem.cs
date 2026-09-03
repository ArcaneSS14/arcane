using Content.Shared._Arcane.Teleportation;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server._Arcane.Teleportation;

/// <summary>
///     This is a drop-in replacement for the upstream <c>ScramOnTriggerSystem</c> that only teleports to
///     a free, non-space tile on the same grid the user is standing on, at least <see cref="MinTeleportDistance"/>
///     tiles away. It also handles users that are buckled or stuffed into a container (e.g. a locker).
///     Uniformly random spots are sampled in the teleport range until one satisfies every condition;
///     candidates that fail the conditions are skipped.
///
///     The action event is only marked handled on a successful teleport, so the action does not spend a
///     charge or start a cooldown when no valid destination can be found.
/// </summary>
public sealed class ScramImplantSystem : SharedScramImplantSystem
{
    private const float MinTeleportDistance = 20f;
    private const float MaxTeleportDistance = 200f;

    /// <summary>
    ///     How many random candidate locations are sampled before giving up. Keeps the search bounded so a
    ///     destination is not spun up forever on a grid with no valid tile.
    /// </summary>
    private const int TeleportAttempts = 10;

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override bool TryEscapeTeleport(EntityUid user, SoundSpecifier teleportSound)
    {
        // No teleport when the user is in space (no grid underneath), and no free-tile check without a body.
        var gridUid = Transform(user).GridUid;
        if (gridUid is not { } uid || !TryComp<PhysicsComponent>(user, out var physics)
            || !TryComp<MapGridComponent>(uid, out var grid))
            return false;

        // Find a valid destination first so a failed search has no side effects (and spends no charge).
        var userWorldPos = _transform.GetWorldPosition(user);
        var collisionMask = (CollisionGroup) physics.CollisionMask;

        // Sample uniformly random spots in the teleport range until one satisfies every condition: on the
        // same grid, not space, not blocked. Candidates that fail the conditions are skipped; if no
        // candidate fits after <see cref="TeleportAttempts"/> tries the teleport is cancelled so the
        // action is not spent.
        EntityCoordinates? targetCoords = null;
        for (var i = 0; i < TeleportAttempts; i++)
        {
            var distance = MinTeleportDistance + (MaxTeleportDistance - MinTeleportDistance) * _random.NextFloat();
            var candidateWorldPos = userWorldPos + _random.NextAngle().ToVec() * distance;

            var tileIndices = _map.WorldToTile(uid, grid, candidateWorldPos);

            if (!_map.TryGetTileRef(uid, grid, tileIndices, out var tileRef))
                continue;

            // Never teleport into open space or into a blocked tile.
            if (_turf.IsSpace(tileRef) || _turf.IsTileBlocked(tileRef, collisionMask))
                continue;

            targetCoords = _turf.GetTileCenter(tileRef);
            break;
        }

        if (targetCoords is not { } coords)
            return false;

        if (TryComp<BuckleComponent>(user, out var buckle) && buckle.Buckled
            && !_buckle.TryUnbuckle(user, null, buckle, false))
            return false;

        if (TryComp<PullableComponent>(user, out var pull) && _pulling.IsPulled(user, pull))
            _pulling.TryStopPull(user, pull, ignoreGrab: true);

        if (TryComp<PullerComponent>(user, out var puller) && TryComp<PullableComponent>(puller.Pulling, out var pullable))
            _pulling.TryStopPull(puller.Pulling.Value, pullable, ignoreGrab: true);

        if (_container.TryGetContainingContainer(user, out var container))
        {
            // Yank the user out of a locker/container in one step, placing them at the destination.
            _container.Remove((user, Transform(user), MetaData(user)), container, force: true, destination: coords);
        }
        else
        {
            _transform.SetCoordinates(user, coords);
        }

        // A single arrival sound at the destination, only once the user has actually been teleported.
        _audio.PlayPvs(teleportSound, coords);
        return true;
    }
}
