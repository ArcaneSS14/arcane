// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.CCVar;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Effects.Step;
using Content.Shared._Arcane.OfferItem;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Medical.Healing;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
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
    [Dependency] private readonly INetManager _net = default!;
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
        // An empty-hand click would otherwise unbuckle the patient from the operating table.
        SubscribeLocalEvent<SurgeryTargetComponent, InteractHandEvent>(OnInteractHand, before: [typeof(SharedBuckleSystem)]);
        SubscribeAllEvent<SurgeryToolOptionPickedEvent>(OnOptionPicked);
        SubscribeLocalEvent<SurgeryPlanComponent, SurgeryStepEvent>(OnPlanStep);
        Subs.BuiEvents<SurgeryTargetComponent>(SurgeryUIKey.Key, subs => subs.Event<SurgeryPlanBuiMsg>(OnPlanMessage));
        Subs.CVar(_config, SurgeryCVars.CanOperateOnSelf, value => _canOperateOnSelf = value, true);
    }

    private void OnInteractUsing(Entity<SurgeryTargetComponent> ent, ref InteractUsingEvent args)
    {
        var user = args.User;
        var tool = args.Used;
        if (args.Handled
            || IsOffering(user)
            || !CanOperate(ent, user, IsSurgicalTool(tool))
            || !TryGetTargetPart(ent, user, out var part, out var missing))
            return;

        var options = GetPlannedOption(ent, part, missing, user, tool) is { } planned
            ? new List<SurgeryToolOption> { planned }
            : GetToolOptions(ent, part, user, tool, missing);

        // Surgery tools may have their own use on a patient, so a click with nothing to do is left to them.
        if (options.Count == 0)
        {
            // A held part would otherwise be fed to the patient, so a part meant for attaching never falls through.
            if (TryComp(tool, out BodyPartComponent? held) && AcceptsHeldPart(ent, part, missing, held) is { } fits)
            {
                args.Handled = true;
                if (_timing.IsFirstTimePredicted)
                {
                    _popup.PopupClient(fits ? GetHint(ent, part, user) : Loc.GetString("surgery-tool-wrong-part", ("part", tool)),
                        ent,
                        user);
                }

                return;
            }

            if (IsSurgicalTool(tool) && _timing.IsFirstTimePredicted)
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
            || !TryGetTargetPart(ent, args.User, out var part, out var missing)
            || GetPlannedOption(ent, part, missing, args.User, args.User) is not { } planned)
            return;

        args.Handled = true;
        // Putting an item into a cavity needs one in hand, so an empty hand does nothing and keeps the plan.
        if (_timing.IsFirstTimePredicted && !IsCavityInsert(planned.Step))
            Perform(ent, part, args.User, new List<SurgeryToolOption> { planned });
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

        var tool = _hands.GetActiveItemOrSelf(user);
        if (tool != user && !_actionBlocker.CanUseHeldEntity(user, tool))
            return;

        // The patient may have changed since the options were shown, so the pick has to still be one of them.
        var missing = TryGetTargetPart(body, user, out var targeted, out var slot) && targeted == part ? slot : null;
        var option = msg.Option;
        if (GetPlannedOption(body, part.Value, missing, user, tool) != option
            && !IsToolOption(body, part.Value, user, tool, option))
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

        if (args.Surgery is not { } surgery)
        {
            if (CompOrNull<SurgeryPlanComponent>(args.Actor)?.Part == part)
                SetPlan(args.Actor, null, null);
            return;
        }

        if (_surgery.GetValidSurgeries(ent, part.Value).Contains(surgery))
            SetPlan(args.Actor, part, surgery);
    }

    /// <summary>
    /// Steps the tool can perform next on the part, merged when several surgeries share a step, or steps with the same effect, that does not depend on them.
    /// With <paramref name="missing"/> set, only surgeries attaching that part to the empty slot are considered.
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
                || GetNextStep(body, part, surgery, user, tool) is not { } next)
                continue;

            EntityUid? owner = DependsOnSurgery(next.StepEnt) ? next.Owner : null;
            var index = options.FindIndex(o => o.Owner == owner
                && (o.Option.Step == next.Step
                    || owner == null
                    && _surgery.GetSingleton(o.Option.Step) is { } shown
                    && (Covers(body, part, shown, next.StepEnt) || Covers(body, part, next.StepEnt, shown))));
            if (index < 0)
            {
                options.Add((owner, new SurgeryToolOption(next.OwnerId, next.Step, surgeryId)));
                continue;
            }

            var option = options[index].Option;
            if (option.Step != next.Step
                && _surgery.GetSingleton(option.Step) is { } kept
                && !Covers(body, part, kept, next.StepEnt))
                option = option with { Surgery = next.OwnerId, Step = next.Step };

            if (option.Target != surgeryId)
                option = option with { Target = null };

            options[index] = (owner, option);
        }

        return options.ConvertAll(o => o.Option);
    }

    /// <summary>
    /// The part the user is targeting on the body, or the parent holding its slot when that part is missing or
    /// still being reattached.
    /// </summary>
    public bool TryGetTargetPart(EntityUid body,
        EntityUid user,
        out EntityUid part,
        out (BodyPartType Type, BodyPartSymmetry Symmetry)? missing)
    {
        var (type, symmetry) = _body.ConvertTargetBodyPart(CompOrNull<TargetingComponent>(user)?.Target ?? TargetBodyPart.Chest);
        return TryGetPart(body, type, symmetry, out part, out missing);
    }

    private bool TryGetPart(EntityUid body,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        out EntityUid part,
        out (BodyPartType Type, BodyPartSymmetry Symmetry)? missing)
    {
        missing = null;

        foreach (var (id, _) in _body.GetBodyChildrenOfType(body, type, symmetry: symmetry))
        {
            // A reattached part is finished by its attach surgery, which lives on the parent.
            if (HasComp<BodyPartReattachedComponent>(id) && _body.GetParentPartOrNull(id) is { } parent)
            {
                part = parent;
                missing = (type, symmetry);
                return true;
            }

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

        // A hand or foot lost with its limb has no slot left on the body, so the limb's slot is used.
        if (type is BodyPartType.Hand or BodyPartType.Foot)
            return TryGetPart(body, type == BodyPartType.Hand ? BodyPartType.Arm : BodyPartType.Leg, symmetry, out part, out missing);

        part = default;
        return false;
    }

    public void SetPlan(EntityUid user, EntityUid? part, EntProtoId? surgery)
    {
        if (part == null && surgery == null && !HasComp<SurgeryPlanComponent>(user))
            return;

        var plan = EnsureComp<SurgeryPlanComponent>(user);
        if (plan.Part == part && plan.Surgery == surgery)
            return;

        plan.Part = part;
        plan.Surgery = surgery;
        Dirty(user, plan);
    }

    // The menu is sent by the server, since the client may predict a different set of options.
    private void Perform(EntityUid body, EntityUid part, EntityUid user, List<SurgeryToolOption> options)
    {
        if (options.Count == 1 && !NeedsConfirmation(options[0].Step))
            DoOption(body, part, user, options[0]);
        else if (_net.IsServer)
            RaiseNetworkEvent(new SurgeryToolOptionsEvent(GetNetEntity(part), options), user);
    }

    private void DoOption(EntityUid body, EntityUid part, EntityUid user, SurgeryToolOption option)
    {
        if (option.Target is { } target)
            SetPlan(user, part, target);

        _surgery.TryDoSurgeryStep(body, part, user, option.Surgery, option.Step, out _);
    }

    // Tool-less steps take whatever is in hand and may start over once undone, so their plan ends with the surgery's
    // last step. Kept until it is done, as a refused, interrupted or ineffective step is reached again only through it.
    private void OnPlanStep(Entity<SurgeryPlanComponent> ent, ref SurgeryStepEvent args)
    {
        if (args.Complete
            && ent.Comp.Part == args.Part
            && !HasToolRequirement(args.Step)
            && TryComp(args.Surgery, out SurgeryComponent? surgery)
            && surgery.Steps.Count > 0
            && surgery.Steps[^1].Id == Prototype(args.Step)?.ID)
            SetPlan(ent, null, null);
    }

    private SurgeryToolOption? GetPlannedOption(EntityUid body,
        EntityUid part,
        (BodyPartType Type, BodyPartSymmetry Symmetry)? missing,
        EntityUid user,
        EntityUid tool)
    {
        if (!TryComp(user, out SurgeryPlanComponent? plan)
            || plan.Part != part
            || plan.Surgery is not { } planned
            || !_surgery.GetValidSurgeries(body, part).Contains(planned)
            || _surgery.GetSingleton(planned) is not { } surgery
            || missing is { } slot && !AttachesPart(surgery, slot.Type, slot.Symmetry)
            || GetNextStep(body, part, surgery, user, tool, fromPlan: true) is not { } next)
            return null;

        return new SurgeryToolOption(next.OwnerId, next.Step, planned);
    }

    // Checked against the owning surgery directly, since merged options may name any of the surgeries sharing the step.
    private bool IsToolOption(EntityUid body, EntityUid part, EntityUid user, EntityUid tool, SurgeryToolOption option)
    {
        return _surgery.GetValidSurgeries(body, part).Contains(option.Surgery)
            && _surgery.GetSingleton(option.Surgery) is { } surgery
            && GetNextStep(body, part, surgery, user, tool) is { } next
            && next.OwnerId == option.Surgery
            && next.Step == option.Step;
    }

    private string GetHint(EntityUid body, EntityUid part, EntityUid user)
    {
        if (TryComp(user, out SurgeryPlanComponent? plan)
            && plan.Part == part
            && plan.Surgery is { } planned
            && _surgery.GetSingleton(planned) is { } surgery
            && GetNextStep(body, part, surgery, user) is { } next)
        {
            if (GetToolName(next.StepEnt) is not { } tool)
            {
                return Loc.GetString("surgery-tool-next-step-no-tool",
                    ("surgery", Name(surgery)),
                    ("step", Name(next.StepEnt)));
            }

            return Loc.GetString("surgery-tool-next-step",
                ("surgery", Name(surgery)),
                ("step", Name(next.StepEnt)),
                ("tool", tool));
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

    // With a tool given, only a step it can perform counts as next.
    private (EntityUid Owner, EntProtoId OwnerId, EntProtoId Step, EntityUid StepEnt)? GetNextStep(EntityUid body,
        EntityUid part,
        EntityUid surgery,
        EntityUid user,
        EntityUid? tool = null,
        bool fromPlan = false)
    {
        if (_surgery.GetNextStep(body, part, surgery, user) is not { } next
            || Prototype(next.Surgery)?.ID is not { } ownerId)
            return null;

        // In any order a negative index marks the last incomplete step, and earlier ones may be open too.
        var steps = next.Surgery.Comp.Steps;
        var anyOrder = next.Step < 0;
        var (first, last) = anyOrder ? (0, -next.Step - 1) : (next.Step, next.Step);
        for (var i = first; i <= last; i++)
        {
            if (anyOrder && _surgery.IsStepComplete(body, part, steps[i], next.Surgery)
                || _surgery.GetSingleton(steps[i]) is not { } stepEnt
                || tool is { } held && !CanPerformWith(next.Surgery, stepEnt, held, fromPlan))
                continue;

            return (next.Surgery.Owner, ownerId, steps[i], stepEnt);
        }

        return null;
    }

    private bool CanPerformWith(EntityUid surgery, EntityUid step, EntityUid tool, bool fromPlan)
    {
        return (AcceptsTool(step, tool) || fromPlan && !HasToolRequirement(step) && CanUseWithoutTool(tool))
            && FitsHeldItem(surgery, step, tool);
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

    // Steps of different surgeries, like an incision and a careful incision, are one choice when doing one also completes
    // the other and they differ only in how much they hurt.
    private bool Covers(EntityUid body, EntityUid part, EntityUid step, EntityUid other)
    {
        if (!TryComp(step, out SurgeryStepComponent? stepComp)
            || !TryComp(other, out SurgeryStepComponent? otherComp)
            || stepComp.AddOrganOnAdd != null || stepComp.RemoveOrganOnAdd != null
            || otherComp.AddOrganOnAdd != null || otherComp.RemoveOrganOnAdd != null
            || !GetEffectTypes(step).SetEquals(GetEffectTypes(other)))
            return false;

        var covered = GetChanges(body, part, otherComp);
        return covered.Count > 0 && covered.IsSubsetOf(GetChanges(body, part, stepComp));
    }

    private HashSet<Type> GetEffectTypes(EntityUid step)
    {
        var types = new HashSet<Type>();
        foreach (var comp in EntityManager.GetComponents(step))
        {
            if (comp is not (SurgeryStepComponent or SurgeryDamageChangeEffectComponent or SurgeryStepPainInflicterComponent))
                types.Add(comp.GetType());
        }

        return types;
    }

    private HashSet<(EntityUid Target, Type Type, bool Add)> GetChanges(EntityUid body, EntityUid part, SurgeryStepComponent step)
    {
        var changes = new HashSet<(EntityUid, Type, bool)>();
        AddChanges(changes, part, step.Add, true);
        AddChanges(changes, part, step.Remove, false);
        AddChanges(changes, body, step.BodyAdd, true);
        AddChanges(changes, body, step.BodyRemove, false);
        return changes;
    }

    private void AddChanges(HashSet<(EntityUid, Type, bool)> changes, EntityUid target, ComponentRegistry? registry, bool add)
    {
        if (registry == null)
            return;

        foreach (var entry in registry.Values)
        {
            var type = entry.Component.GetType();
            if (EntityManager.HasComponent(target, type) != add)
                changes.Add((target, type, add));
        }
    }

    private bool IsCavityInsert(EntProtoId step)
    {
        return _surgery.GetSingleton(step) is { } stepEnt
            && TryComp(stepEnt, out SurgeryStepCavityEffectComponent? cavity)
            && cavity.Action == "Insert";
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

    // Null when no attach surgery on the part could take a part, otherwise whether one takes the held one.
    private bool? AcceptsHeldPart(EntityUid body,
        EntityUid part,
        (BodyPartType Type, BodyPartSymmetry Symmetry)? missing,
        BodyPartComponent held)
    {
        bool? accepts = null;
        foreach (var surgeryId in _surgery.GetValidSurgeries(body, part))
        {
            if (_surgery.GetSingleton(surgeryId) is not { } surgery
                || !TryComp(surgery, out SurgeryPartRemovedConditionComponent? removed)
                || missing is { } slot && !AttachesPart(surgery, slot.Type, slot.Symmetry))
                continue;

            if (removed.Part == held.PartType && (removed.Symmetry == null || removed.Symmetry == held.Symmetry))
                return true;

            accepts = false;
        }

        return accepts;
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

    // Organs and parts held for insertion must reach their own steps instead of a cavity implant.
    private bool CanUseWithoutTool(EntityUid held)
    {
        return !IsSurgicalTool(held) && !HasComp<OrganComponent>(held) && !HasComp<BodyPartComponent>(held);
    }

    private bool IsOffering(EntityUid user)
    {
        return TryComp(user, out OfferItemComponent? offer) && offer.IsInOfferMode;
    }
}
