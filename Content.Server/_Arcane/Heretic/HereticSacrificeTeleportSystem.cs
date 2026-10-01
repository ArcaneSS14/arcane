using Content.Server._Arcane.Heretic.Components;
using Content.Server.Respawn;
using Content.Server.Station.Systems;
using Content.Shared.Administration.Systems;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Drunk;
using Content.Shared.Jittering;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared._Shitmed.Targeting;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server._Arcane.Heretic;

public sealed class HereticSacrificeTeleportSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDrunkSystem _drunk = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedStutteringSystem _stutter = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpecialRespawnSystem _respawn = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private const int TileAttempts = 30;

    private static readonly TimeSpan Stun = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan Jitter = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan Stutter = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan Drunk = TimeSpan.FromSeconds(60);

    private static readonly DamageSpecifier Damage = new()
    {
        DamageDict = { ["Heat"] = 50, ["Cellular"] = 70 },
    };

    private static readonly SoundSpecifier DepartureSound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
    private static readonly SoundSpecifier ArrivalSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    public bool TryTeleportVictim(EntityUid victim)
    {
        if (!HasComp<MobStateComponent>(victim) || !TryFindDestination(victim, out var coords))
            return false;

        EnsureComp<HereticSacrificedComponent>(victim);

        _audio.PlayPvs(DepartureSound, victim);
        Teleport(victim, coords);
        _audio.PlayPvs(ArrivalSound, victim);

        _rejuvenate.PerformRejuvenate(victim);
        _damageable.TryChangeDamage(victim, new DamageSpecifier(Damage), true, false, targetPart: TargetBodyPart.All, canMiss: false);

        if (_mind.TryGetMind(victim, out var mindId, out var mind) && mind.VisitingEntity != null)
            _mind.UnVisit(mindId, mind);

        _stun.TryUpdateParalyzeDuration(victim, Stun);
        _jitter.DoJitter(victim, Jitter, true);
        _stutter.DoStutter(victim, Stutter, true);
        _drunk.TryApplyDrunkenness(victim, Drunk);

        _popup.PopupEntity(Loc.GetString("heretic-sacrifice-teleport"), victim, victim, PopupType.LargeCaution);
        return true;
    }

    private bool TryFindDestination(EntityUid victim, out EntityCoordinates coords)
    {
        coords = EntityCoordinates.Invalid;

        var station = _station.GetOwningStation(victim);
        if (station == null)
        {
            var stations = _station.GetStations();
            if (stations.Count == 0)
                return false;

            station = stations[0];
        }

        if (_station.GetLargestGrid(station.Value) is not { } grid || Transform(grid).MapUid is not { } map)
            return false;

        return _respawn.TryFindRandomTile(grid, map, TileAttempts, out coords);
    }

    private void Teleport(EntityUid uid, EntityCoordinates coords)
    {
        if (TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled)
            _buckle.Unbuckle((uid, buckle), null);

        if (TryComp<PullableComponent>(uid, out var pullable) && _pulling.IsPulled(uid, pullable))
            _pulling.TryStopPull(uid, pullable, ignoreGrab: true);

        if (TryComp<PullerComponent>(uid, out var puller) && TryComp<PullableComponent>(puller.Pulling, out var pulled))
            _pulling.TryStopPull(puller.Pulling.Value, pulled, ignoreGrab: true);

        if (_container.TryGetContainingContainer(uid, out var container))
            _container.Remove((uid, Transform(uid), MetaData(uid)), container, force: true, destination: coords);
        else
            _transform.SetCoordinates(uid, coords);
    }
}
