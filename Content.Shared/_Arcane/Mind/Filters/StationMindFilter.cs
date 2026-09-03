using Content.Shared.Roles.Jobs;
using Content.Shared.Station;

namespace Content.Shared.Mind.Filters;

/// <summary>
///     A mind filter that keeps only station crew: minds whose owned entity is on a station
///     grid and that hold a selectable station job.
/// </summary>
public sealed partial class StationMindFilter : MindFilter
{
    protected override bool ShouldRemove(Entity<MindComponent> mind, EntityUid? exclude, IEntityManager entMan, SharedMindSystem mindSys)
    {
        // not on a station grid (e.g. Lavaland)
        if (mind.Comp.OwnedEntity is not { } mob)
            return true;

        var stationSystem = entMan.System<SharedStationSystem>();
        if (stationSystem.GetOwningStation(mob) == null)
            return true;

        // must hold a job that's selectable in the preferences menu
        var jobSystem = entMan.System<SharedJobSystem>();
        return !jobSystem.MindTryGetJob(mind.Owner, out var job) || !job.SetPreference;
    }
}
