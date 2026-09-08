// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking.Rules;
using Content.Server.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Handles assinging a target to an objective entity with <see cref="TargetObjectiveComponent"/> using different components.
/// These can be combined with condition components for objective completions in order to create a variety of objectives.
/// </summary>
public sealed class PickObjectiveTargetSystem : EntitySystem
{
    [Dependency] private readonly TargetObjectiveSystem _target = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    // Arcane-Start
    [Dependency] private readonly SharedJobSystem _job = default!;

    public static readonly ProtoId<JobPrototype>[] SecurityTargetJobs =
    {
        "HeadOfSecurity",
        "Warden",
        "Detective",
        "Brigmedic",
        "SecurityOfficer",
        "SecurityCadet",
    };
    // Arcane-End

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PickSpecificPersonComponent, ObjectiveAssignedEvent>(OnSpecificPersonAssigned);
        SubscribeLocalEvent<PickRandomPersonComponent, ObjectiveAssignedEvent>(OnRandomPersonAssigned);
    }

    private void OnSpecificPersonAssigned(Entity<PickSpecificPersonComponent> ent, ref ObjectiveAssignedEvent args)
    {
        // invalid objective prototype
        if (!TryComp<TargetObjectiveComponent>(ent.Owner, out var target))
        {
            args.Cancelled = true;
            return;
        }

        // target already assigned
        if (target.Target != null)
            return;

        if (args.Mind.OwnedEntity == null)
        {
            args.Cancelled = true;
            return;
        }

        var user = args.Mind.OwnedEntity.Value;
        if (!TryComp<TargetOverrideComponent>(user, out var targetComp) || targetComp.Target == null)
        {
            args.Cancelled = true;
            return;
        }

        _target.SetTarget(ent.Owner, targetComp.Target.Value);
    }

    private void OnRandomPersonAssigned(Entity<PickRandomPersonComponent> ent, ref ObjectiveAssignedEvent args)
    {
        // invalid objective prototype
        if (!TryComp<TargetObjectiveComponent>(ent, out var target))
        {
            args.Cancelled = true;
            return;
        }

        // target already assigned
        if (target.Target != null)
            return;

        // Arcane-Start
        Entity<MindComponent>? picked = HasComp<KillPersonConditionComponent>(ent.Owner)
            ? _mind.WeightedPickFromPool(ent.Comp.Pool, ent.Comp.Filters, args.MindId, IsSecurityTargetWeight)
            : _mind.PickFromPool(ent.Comp.Pool, ent.Comp.Filters, args.MindId);
        // Arcane-End

        // couldn't find a target :(
        if (picked is not { } valid) // Arcane-Edit
        {
            args.Cancelled = true;
            return;
        }

        _target.SetTarget(ent, valid, target); // Arcane-Edit
    }

    // Arcane-Start
    private float IsSecurityTargetWeight(Entity<MindComponent> mind)
    {
        if (!_job.MindTryGetJob(mind.Owner, out var job))
            return 1f;

        foreach (var jobId in SecurityTargetJobs)
        {
            if (job.ID == jobId)
                return 3f;
        }

        return 1f;
    }
    // Arcane-End
}
