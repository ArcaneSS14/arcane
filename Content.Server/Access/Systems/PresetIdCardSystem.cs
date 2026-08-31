// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Access.Components;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Server.Access.Systems;

public sealed class PresetIdCardSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IdCardSystem _cardSystem = default!;
    [Dependency] private readonly SharedAccessSystem _accessSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PresetIdCardComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(PlayerJobsAssigned);
    }

    private void PlayerJobsAssigned(RulePlayerJobsAssignedEvent ev)
    {
        // Go over all ID cards and make sure they're correctly configured for extended access.

        var query = EntityQueryEnumerator<PresetIdCardComponent>();
        while (query.MoveNext(out var uid, out var card))
        {
            var station = _stationSystem.GetOwningStation(uid);

            // If we're not on an extended access station, the ID is already configured correctly from MapInit.
            if (station == null || !TryComp<StationJobsComponent>(station.Value, out var jobsComp) || !jobsComp.ExtendedAccess)
                continue;

            SetupIdAccess(uid, card, true);
            SetupIdName(uid, card);
        }
    }

    private void OnMapInit(EntityUid uid, PresetIdCardComponent id, MapInitEvent args)
    {
        // If a preset ID card is spawned on a station at setup time,
        // the station may not exist,
        // or may not yet know whether it is on extended access (players not spawned yet).
        // PlayerJobsAssigned makes sure extended access is configured correctly in that case.

        var station = _stationSystem.GetOwningStation(uid);
        var extended = false;

        // Station not guaranteed to have jobs (e.g. nukie outpost).
        if (TryComp(station, out StationJobsComponent? stationJobs))
            extended = stationJobs.ExtendedAccess;

        SetupIdAccess(uid, id, extended);
        SetupIdName(uid, id);
    }

    private void SetupIdName(EntityUid uid, PresetIdCardComponent id)
    {
        if (id.IdName == null)
            return;
        _cardSystem.TryChangeFullName(uid, id.IdName);
    }

    private void SetupIdAccess(EntityUid uid, PresetIdCardComponent id, bool extended)
    {
        if (id.JobName == null)
            return;

        if (!_prototypeManager.TryIndex(id.JobName.Value, out JobPrototype? job)) // Arcane-Edit: id.JobName > id.JobName.Value
        {
            Log.Error($"Invalid job id ({id.JobName}) for preset card");
            return;
        }

        _accessSystem.SetAccessToJob(uid, job, extended);

        // _cardSystem.TryChangeJobTitle(uid, job.LocalizedName); // Arcane-Edit
        // Arcane-Start
        if (!TryComp<IdCardComponent>(uid, out var card))
        {
            Log.Warning($"Entity {uid} does not have IdCardComponent, skipping title setup.");
            return;
        }

        string? titleToSet = null;

        if (id.AlternateTitleId != null &&
            _prototypeManager.TryIndex(id.AlternateTitleId.Value, out JobAlternateTitlePrototype? altTitle))
        {
            titleToSet = altTitle.LocalizedName(Gender.Neuter);
        }
        // Disabled this so new cards have a default job name, not first alternative.
        // else if (job.AlternateTitles != null && job.AlternateTitles.Count > 0)
        // {
        //     JobAlternateTitlePrototype? altFromJob = null;
        //     foreach (var altId in job.AlternateTitles)
        //     {
        //         if (_prototypeManager.TryIndex(altId, out var proto))
        //         {
        //             altFromJob = proto;
        //             break;
        //         }
        //     }

        //     titleToSet = altFromJob?.LocalizedName(Gender.Neuter) ?? job.LocalizedName;
        // }
        else
        {
            titleToSet = job.LocalizedName;
        }

        if (!string.IsNullOrEmpty(titleToSet))
            _cardSystem.TryChangeJobTitle(uid, titleToSet);
        // Arcane-End

        _cardSystem.TryChangeJobDepartment(uid, job);

        if (!string.IsNullOrEmpty(job.Icon) && _prototypeManager.Resolve(job.Icon, out var jobIcon)) // Arcane-Edit
            _cardSystem.TryChangeJobIcon(uid, jobIcon);
    }
}
