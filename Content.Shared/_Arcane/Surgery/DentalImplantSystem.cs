// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Arcane.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Components;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.Surgery;

public sealed class DentalImplantSystem : EntitySystem
{
    public const string ContainerId = "dental_implants";

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly StomachSystem _stomach = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryDentalImplantStepComponent, SurgeryStepEvent>(OnStep);
        SubscribeLocalEvent<SurgeryDentalImplantStepComponent, SurgeryStepCompleteCheckEvent>(OnCompleteCheck);
        SubscribeLocalEvent<SurgeryDentalImplantConditionComponent, SurgeryValidEvent>(OnValid);
        SubscribeLocalEvent<DentalImplantComponent, ActivateDentalPillActionEvent>(OnActivate);
        SubscribeLocalEvent<DentalImplantComponent, DentalPillDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<SurgeryTargetComponent, BodyPartAddedEvent>(OnPartAttached);
        SubscribeLocalEvent<SurgeryTargetComponent, BodyPartRemovedEvent>(OnPartDetached);
    }

    private void OnPartAttached(Entity<SurgeryTargetComponent> ent, ref BodyPartAddedEvent args)
    {
        if (!_net.IsServer)
            return;

        foreach (var pill in GetPills(args.Part))
        {
            if (TryComp<DentalImplantComponent>(pill, out var implant) && implant.Action is { } action)
                _actions.GrantContainedAction(ent.Owner, pill, action);
        }
    }

    private void OnPartDetached(Entity<SurgeryTargetComponent> ent, ref BodyPartRemovedEvent args)
    {
        if (!_net.IsServer)
            return;

        foreach (var pill in GetPills(args.Part))
        {
            _actions.RemoveProvidedActions(ent.Owner, pill);
        }
    }

    private void OnStep(Entity<SurgeryDentalImplantStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer)
            return;

        if (ent.Comp.Remove)
        {
            if (GetPill(args.Part) is not { } pill)
                return;

            _actions.RemoveProvidedActions(args.Body, pill);
            if (TryComp<DentalImplantComponent>(pill, out var removed))
                QueueDel(removed.Action);
            RemComp<DentalImplantComponent>(pill);
            _hands.PickupOrDrop(args.User, pill);
            return;
        }

        // A few pills per patient, unlike tgstation's one per tooth.
        if (GetPills(args.Part).Count >= ent.Comp.MaxPills
            || _hands.GetActiveItem(args.User) is not { } held
            || !HasComp<PillComponent>(held))
            return;

        var container = _container.EnsureContainer<Container>(args.Part, ContainerId);
        if (!_container.Insert(held, container))
            return;

        var implant = EnsureComp<DentalImplantComponent>(held);
        implant.Action = _actions.AddAction(args.Body, ent.Comp.Action, held);
        if (implant.Action is { } action)
            _metaData.SetEntityName(action, Loc.GetString("dental-implant-action-name", ("pill", held)));
    }

    private void OnCompleteCheck(Entity<SurgeryDentalImplantStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        var count = GetPills(args.Part).Count;
        if (ent.Comp.Remove ? count > 0 : count < ent.Comp.MaxPills)
            args.Cancelled = true;
    }

    private void OnValid(Entity<SurgeryDentalImplantConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (GetPill(args.Part) == null)
            args.Cancelled = true;
    }

    private void OnActivate(Entity<DentalImplantComponent> ent, ref ActivateDentalPillActionEvent args)
    {
        var user = args.Performer;
        if (args.Handled || !IsInMouth(ent, user) || !CanBite(user))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("dental-implant-grind-self"), user, user);
        _popup.PopupEntity(
            Loc.GetString("dental-implant-grind-others", ("user", user)),
            user,
            Filter.PvsExcept(user, entityManager: EntityManager),
            true);

        if (!_mobState.IsCritical(user))
        {
            Burst(ent, user);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.CriticalDelay, new DentalPillDoAfterEvent(), ent, user, ent)
        {
            BreakOnMove = false,
            RequireCanInteract = false,
        });
    }

    private void OnDoAfter(Entity<DentalImplantComponent> ent, ref DentalPillDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !IsInMouth(ent, args.User) || !CanBite(args.User))
            return;

        args.Handled = true;
        Burst(ent, args.User);
    }

    private void Burst(EntityUid pill, EntityUid user)
    {
        if (!_net.IsServer)
            return;

        if (!TryComp<EdibleComponent>(pill, out var edible)
            || !_solution.TryGetSolution(pill, edible.Solution, out var soln, out var solution))
            return;

        foreach (var stomach in _body.GetBodyOrganEntityComps<StomachComponent>(user))
        {
            if (!_stomach.TryTransferSolution(stomach, solution, stomach.Comp1))
                continue;

            _solution.RemoveAllSolution(soln.Value);
            _popup.PopupEntity(Loc.GetString("dental-implant-burst", ("pill", pill)), user, user);
            _adminLog.Add(LogType.Ingestion, LogImpact.Medium, $"{ToPrettyString(user):user} burst a dental implant {ToPrettyString(pill):pill}");
            _actions.RemoveProvidedActions(user, pill);
            QueueDel(pill);
            return;
        }
    }

    private bool CanBite(EntityUid user)
    {
        return (_mobState.IsAlive(user) || _mobState.IsSoftCritical(user))
            && !HasComp<SleepingComponent>(user)
            && !_status.HasEffectComp<ForcedSleepingStatusEffectComponent>(user)
            && (!TryComp<ConsciousnessComponent>(user, out var consciousness) || consciousness.IsConscious);
    }

    // The head can be cut off with the pill still in it.
    private bool IsInMouth(EntityUid pill, EntityUid user)
    {
        return _container.TryGetContainingContainer(pill, out var container)
            && container.ID == ContainerId
            && TryComp<BodyPartComponent>(container.Owner, out var part)
            && part.Body == user;
    }

    private EntityUid? GetPill(EntityUid part)
    {
        var pills = GetPills(part);
        return pills.Count > 0 ? pills[^1] : null;
    }

    private IReadOnlyList<EntityUid> GetPills(EntityUid part)
    {
        return _container.TryGetContainer(part, ContainerId, out var container)
            ? container.ContainedEntities
            : [];
    }
}

public sealed partial class ActivateDentalPillActionEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public sealed partial class DentalPillDoAfterEvent : SimpleDoAfterEvent { }
