using Content.Server.Actions;
using Content.Server.Wagging;
using Content.Server._Arcane.Wagging;
using Content.Shared._Shitmed.Humanoid.Events;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Wagging;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Wagging;

/// <summary>
/// Grants ActionToggleWagging to mobs with configured tail markings (e.g. HorseTail).
/// </summary>
public sealed class WaggingTailGrantSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly WaggingSystem _wagging = default!;

    private const string AnimatedSuffix = "Animated";

     private readonly  HashSet<ProtoId<MarkingPrototype>> _eligibleTailMarkings = new();

    public override void Initialize()
    {
        base.Initialize();

        foreach (var grant in _prototype.GetInstances<WaggingTailGrantPrototype>().Values)
        {
            foreach (var marking in grant.TailMarkings)
                _eligibleTailMarkings.Add(marking);
        }

        SubscribeLocalEvent<HumanoidAppearanceComponent, ProfileLoadFinishedEvent>(OnProfileLoaded);
        SubscribeLocalEvent<HumanoidAppearanceComponent, HumanoidMarkingsUpdatedEvent>(OnMarkingsUpdated);
    }

    private void OnProfileLoaded(Entity<HumanoidAppearanceComponent> ent, ref ProfileLoadFinishedEvent args)
    {
        UpdateWaggingGrant(ent, ent.Comp);
    }

    private readonly HashSet<string> _eligibleTailMarkings = new();

    private void OnMarkingsUpdated(Entity<HumanoidAppearanceComponent> ent, ref HumanoidMarkingsUpdatedEvent args)
    {
        UpdateWaggingGrant(ent, ent.Comp);
    }

    private void UpdateWaggingGrant(EntityUid uid, HumanoidAppearanceComponent humanoid)
    {
        var hasEligibleTail = HasEligibleTailMarking(humanoid);
        var markingGranted = HasComp<MarkingGrantedWaggingComponent>(uid);

        if (hasEligibleTail)
        {
            if (!HasComp<WaggingComponent>(uid))
                GrantMarkingWagging(uid);
        }
        else if (markingGranted)
        {
            RevokeMarkingWagging(uid);
        }
    }

    private void GrantMarkingWagging(EntityUid uid)
    {
        var wagging = EnsureComp<WaggingComponent>(uid);
        if (wagging.ActionEntity == null)
            _actions.AddAction(uid, ref wagging.ActionEntity, wagging.Action, uid);

        EnsureComp<MarkingGrantedWaggingComponent>(uid);
    }

    private void RevokeMarkingWagging(EntityUid uid)
    {
        if (TryComp<WaggingComponent>(uid, out var wagging) && wagging.Wagging)
            _wagging.TryToggleWagging(uid, wagging);

        RemComp<WaggingComponent>(uid);
        RemComp<MarkingGrantedWaggingComponent>(uid);
    }

    private bool HasEligibleTailMarking(HumanoidAppearanceComponent humanoid)
    {
        if (!humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Tail, out var markings))
            return false;

        foreach (var marking in markings)
        {
            if (IsEligibleTailMarking(marking.MarkingId))
                return true;
        }

        return false;
    }

    private bool IsEligibleTailMarking(string markingId)
    {
        if (_eligibleTailMarkings.Contains(markingId))
            return true;

        if (markingId.EndsWith(AnimatedSuffix))
        {
            var baseId = markingId[..^AnimatedSuffix.Length];
            if (_eligibleTailMarkings.Contains(baseId))
                return true;
        }

        return false;
    }
}
