// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.CCVar;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Medical.Healing;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Arcane.Surgery;

/// <summary>
/// Performs surgery steps by clicking a lying patient with a tool, on the body part the surgeon is targeting.
/// </summary>
public abstract class SharedSurgeryToolSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSurgerySystem _surgery = default!;

    private bool _canOperateOnSelf;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryTargetComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SurgeryTargetComponent, InteractHandEvent>(OnInteractHand);
        SubscribeAllEvent<SurgeryToolOptionPickedEvent>(OnOptionPicked);
        Subs.BuiEvents<SurgeryTargetComponent>(SurgeryUIKey.Key, subs => subs.Event<SurgeryPlanBuiMsg>(OnPlanMessage));
        Subs.CVar(_config, SurgeryCVars.CanOperateOnSelf, value => _canOperateOnSelf = value, true);
    }

    private void OnInteractUsing(Entity<SurgeryTargetComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        var tool = args.Used;
        var surgical = IsSurgicalTool(tool);
        if (!CanOperate(ent, user, surgical && _timing.IsFirstTimePredicted))
        {
            // A surgical tool has no other use on a patient, so the reason is shown instead of doing nothing.
            args.Handled = surgical;
            return;
        }

        if (!TryGetTargetPart(ent, user, out var part, out var missing))
            return;

        var options = GetPlannedOption(ent, part, user, tool) is { } planned
            ? [planned]
            : GetToolOptions(ent, part, user, tool, missing);

        if (options.Count == 0)
        {
            if (!surgical)
                return;

            args.Handled = true;
            if (_timing.IsFirstTimePredicted)
                _popup.PopupClient(GetHint(ent, part, user), ent, user);
            return;
        }

        args.Handled = true;
        if (_timing.IsFirstTimePredicted)
            Perform(ent, part, user, options);
    }

    // Planned steps that need no tool, like taking an item out of a cavity, are done with an empty hand.
    private void OnInteractHand(Entity<SurgeryTargetComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled
            || !CanOperate(ent, args.User, false)
            || !TryGetTargetPart(ent, args.User, out var part, out _)
            || GetPlannedOption(ent, part, args.User, args.User) is not { } planned)
            return;

        args.Handled = true;
        if (_timing.IsFirstTimePredicted)
            Perform(ent, part, args.User, [planned]);
    }

    private void OnOptionPicked(SurgeryToolOptionPickedEvent msg, EntitySessionEventArgs args)
    {
        if (!_timing.IsFirstTimePredicted
            || args.SenderSession.AttachedEntity is not { } user
            || !TryGetEntity(msg.Part, out var part)
            || !TryComp(part, out BodyPartComponent? partComp)
            || partComp.Body is not { } body
            || !TryComp(body, out SurgeryTargetComponent? target)
            || !_actionBlocker.CanInteract(user, body)
            || !_interaction.InRangeUnobstructed(user, body)
            || !CanOperate((body, target), user, true))
            return;

        // The patient may have changed since the options were shown, so the pick has to still be one of them.
        var tool = _hands.GetActiveItemOrSelf(user);
        var option = msg.Option;
        if (GetPlannedOption(body, part.Value, user, tool) != option && !IsToolOption(body, part.Value, user, tool, option))
            return;

        if (option.Target is { } targetSurgery && !_surgery.GetValidSurgeries(body, part.Value).Contains(targetSurgery))
            option = option with { Target = null };

        DoOption(body, part.Value, user, option);
    }

    private void OnPlanMessage(Entity<SurgeryTargetComponent> ent, ref SurgeryPlanBuiMsg args)
    {
        if (!TryGetEntity(args.Part, out var part)
            || !TryComp(part, out BodyPartComponent? partComp)
            || partComp.Body != ent.Owner)
            return;

        if (args.Surgery is { } surgery && !_surgery.GetValidSurgeries(ent, part.Value).Contains(surgery))
            return;

        SetPlan(args.Actor, args.Surgery == null ? null : part, args.Surgery);
    }

    /// <summary>
    /// Steps the tool can perform next on the part. Surgeries sharing a step whose effect does not depend on the
    /// surgery are merged into one option. With <paramref name="missing"/> set, only surgeries attaching that kind of
    /// part to the empty slot of <paramref name="part"/> are considered.
    /// </summary>
    public List<SurgeryToolOption> GetToolOptions(EntityUid body,
        EntityUid part,
        EntityUid user,
        EntityUid tool,
        (BodyPartType Type, BodyPartSymmetry Symmetry)? missing = null)
    {
        var options = new List<(EntityUid? Owner, SurgeryToolOption Option)>();
        foreach (var surgeryId in _surgery.GetValidSurgeries(body, part))
        {
            if (_surgery.GetSingleton(surgeryId) is not { } surgery
                || missing is { } slot && !AttachesPart(surgery, slot.Type, slot.Symmetry)
                || GetNextStep(body, part, surgery, user) is not { } next
                || !AcceptsTool(next.StepEnt, tool)
                || !FitsHeldItem(next.Owner, next.StepEnt, tool))
                continue;

            EntityUid? owner = DependsOnSurgery(next.StepEnt) ? next.Owner : null;
            var index = options.FindIndex(o => o.Owner == owner && o.Option.Step == next.Step);
            if (index < 0)
                options.Add((owner, new SurgeryToolOption(next.OwnerId, next.Step, surgeryId)));
            else if (options[index].Option.Target != surgeryId)
                options[index] = (owner, options[index].Option with { Target = null });
        }

        return options.ConvertAll(o => o.Option);
    }

    /// <summary>
    /// The part the user is targeting on the body, or the parent holding its empty slot when that part is missing.
    /// </summary>
    public bool TryGetTargetPart(EntityUid body,
        EntityUid user,
        out EntityUid part,
        out (BodyPartType Type, BodyPartSymmetry Symmetry)? missing)
    {
        var (type, symmetry) = _body.ConvertTargetBodyPart(CompOrNull<TargetingComponent>(user)?.Target ?? TargetBodyPart.Chest);
        missing = null;

        foreach (var (id, _) in _body.GetBodyChildrenOfType(body, type, symmetry: symmetry))
        {
            part = id;
            return true;
        }

        foreach (var (id, comp) in _body.GetBodyChildren(body))
        {
            foreach (var (slotId, slot) in comp.Children)
            {
                if (slot.Type != type
                    || slot.Symmetry != symmetry
                    || _container.TryGetContainer(id, SharedBodySystem.GetPartSlotContainerId(slotId), out var container)
                    && container.ContainedEntities.Count > 0)
                    continue;

                part = id;
                missing = (type, symmetry);
                return true;
            }
        }

        part = default;
        return false;
    }

    public void SetPlan(EntityUid user, EntityUid? part, EntProtoId? surgery)
    {
        var plan = EnsureComp<SurgeryPlanComponent>(user);
        if (plan.Part == part && plan.Surgery == surgery)
            return;

        plan.Part = part;
        plan.Surgery = surgery;
        Dirty(user, plan);
    }

    /// <summary>
    /// Lets the user pick one of the steps their tool click could perform, or confirm a single destructive one.
    /// </summary>
    protected virtual void OpenOptions(EntityUid user, EntityUid body, EntityUid part, List<SurgeryToolOption> options)
    {
    }

    private void Perform(EntityUid body, EntityUid part, EntityUid user, List<SurgeryToolOption> options)
    {
        if (options.Count == 1 && !NeedsConfirmation(options[0].Step))
            DoOption(body, part, user, options[0]);
        else
            OpenOptions(user, body, part, options);
    }

    private void DoOption(EntityUid body, EntityUid part, EntityUid user, SurgeryToolOption option)
    {
        if (option.Target is { } target)
            SetPlan(user, part, target);

        _surgery.TryDoSurgeryStep(body, part, user, option.Surgery, option.Step, out _);
    }

    private SurgeryToolOption? GetPlannedOption(EntityUid body, EntityUid part, EntityUid user, EntityUid tool)
    {
        if (!TryComp(user, out SurgeryPlanComponent? plan)
            || plan.Part != part
            || plan.Surgery is not { } planned
            || !_surgery.GetValidSurgeries(body, part).Contains(planned)
            || _surgery.GetSingleton(planned) is not { } surgery
            || GetNextStep(body, part, surgery, user) is not { } next
            || !(AcceptsTool(next.StepEnt, tool) || !HasToolRequirement(next.StepEnt) && !IsSurgicalTool(tool))
            || !FitsHeldItem(next.Owner, next.StepEnt, tool))
            return null;

        return new SurgeryToolOption(next.OwnerId, next.Step, planned);
    }

    // Checked against the owning surgery directly, since merged options may name any of the surgeries sharing the step.
    private bool IsToolOption(EntityUid body, EntityUid part, EntityUid user, EntityUid tool, SurgeryToolOption option)
    {
        return _surgery.GetValidSurgeries(body, part).Contains(option.Surgery)
            && _surgery.GetSingleton(option.Surgery) is { } surgery
            && GetNextStep(body, part, surgery, user) is { } next
            && next.OwnerId == option.Surgery
            && next.Step == option.Step
            && AcceptsTool(next.StepEnt, tool)
            && FitsHeldItem(next.Owner, next.StepEnt, tool);
    }

    private string GetHint(EntityUid body, EntityUid part, EntityUid user)
    {
        if (TryComp(user, out SurgeryPlanComponent? plan)
            && plan.Part == part
            && plan.Surgery is { } planned
            && _surgery.GetSingleton(planned) is { } surgery
            && GetNextStep(body, part, surgery, user) is { } next)
        {
            return Loc.GetString("surgery-tool-next-step",
                ("surgery", Name(surgery)),
                ("step", Name(next.StepEnt)),
                ("tool", GetToolName(next.StepEnt) ?? string.Empty));
        }

        return Loc.GetString("surgery-tool-nothing-to-do", ("part", part));
    }

    private bool CanOperate(Entity<SurgeryTargetComponent> body, EntityUid user, bool popup)
    {
        if (!body.Comp.CanOperate)
            return false;

        if (user == body.Owner && !_canOperateOnSelf)
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("surgery-error-self-surgery"), user, user);
            return false;
        }

        return _surgery.IsLyingDown(body, user, popup);
    }

    private (EntityUid Owner, EntProtoId OwnerId, EntProtoId Step, EntityUid StepEnt)? GetNextStep(EntityUid body,
        EntityUid part,
        EntityUid surgery,
        EntityUid user)
    {
        if (_surgery.GetNextStep(body, part, surgery, user) is not { } next)
            return null;

        // A negative index means steps may be done in any order, and this one is still incomplete.
        var index = next.Step < 0 ? -next.Step - 1 : next.Step;
        var stepId = next.Surgery.Comp.Steps[index];
        if (Prototype(next.Surgery)?.ID is not { } ownerId || _surgery.GetSingleton(stepId) is not { } stepEnt)
            return null;

        return (next.Surgery.Owner, ownerId, stepId, stepEnt);
    }

    // Steps without a tool only run from the plan, otherwise any held item would start them.
    private bool AcceptsTool(EntityUid step, EntityUid tool)
    {
        if (!TryComp(step, out SurgeryStepComponent? comp) || comp.Tool is not { } tools)
            return false;

        foreach (var registry in tools.Values)
        {
            if (EntityManager.HasComponent(tool, registry.Component.GetType()))
                return true;
        }

        return false;
    }

    private bool HasToolRequirement(EntityUid step)
    {
        return TryComp(step, out SurgeryStepComponent? comp) && comp.Tool is { Count: > 0 };
    }

    // Inserting steps accept any organ or part, but only the one the surgery is about gets inserted.
    private bool FitsHeldItem(EntityUid surgery, EntityUid step, EntityUid tool)
    {
        if (HasComp<SurgeryAddOrganStepComponent>(step))
        {
            if (!TryComp(surgery, out SurgeryOrganConditionComponent? organ) || organ.Organ is not { } organs)
                return false;

            foreach (var registry in organs.Values)
            {
                if (!EntityManager.HasComponent(tool, registry.Component.GetType()))
                    return false;
            }

            return true;
        }

        if (HasComp<SurgeryAddPartStepComponent>(step))
        {
            return TryComp(tool, out BodyPartComponent? held)
                && TryComp(surgery, out SurgeryPartRemovedConditionComponent? removed)
                && held.PartType == removed.Part
                && (removed.Symmetry == null || held.Symmetry == removed.Symmetry);
        }

        return true;
    }

    // These steps read the owning surgery to pick the organ, part or slot they act on.
    private bool DependsOnSurgery(EntityUid step)
    {
        return HasComp<SurgeryAddPartStepComponent>(step)
            || HasComp<SurgeryAffixPartStepComponent>(step)
            || HasComp<SurgeryAddOrganStepComponent>(step)
            || HasComp<SurgeryAffixOrganStepComponent>(step)
            || HasComp<SurgeryRemoveOrganStepComponent>(step)
            || HasComp<SurgeryAddOrganSlotStepComponent>(step);
    }

    private bool NeedsConfirmation(EntProtoId step)
    {
        return _surgery.GetSingleton(step) is { } stepEnt && HasComp<SurgeryRemovePartStepComponent>(stepEnt);
    }

    public string? GetToolName(EntityUid step)
    {
        if (!TryComp(step, out SurgeryStepComponent? comp) || comp.Tool is not { } tools)
            return null;

        foreach (var registry in tools.Values)
        {
            if (registry.Component is ISurgeryToolComponent tool)
                return tool.ToolName;
        }

        return null;
    }

    private bool AttachesPart(EntityUid surgery, BodyPartType type, BodyPartSymmetry symmetry)
    {
        return TryComp(surgery, out SurgeryPartRemovedConditionComponent? removed)
            && removed.Part == type
            && (removed.Symmetry == null || removed.Symmetry == symmetry);
    }

    // Healing items double as surgery tools but have their own use on a patient.
    private bool IsSurgicalTool(EntityUid tool)
    {
        return HasComp<SurgeryToolComponent>(tool) && !HasComp<HealingComponent>(tool);
    }
}
