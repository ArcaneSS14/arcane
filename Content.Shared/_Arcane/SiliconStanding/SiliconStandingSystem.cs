using Content.Shared.Actions;
using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.SiliconStanding;

public sealed class SharedSiliconStandingSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconStandingComponent, ToggleSiliconRestingEvent>(OnToggleAction);
        SubscribeLocalEvent<SiliconStandingComponent, UpdateCanMoveEvent>(OnCanMove);
        SubscribeLocalEvent<SiliconStandingComponent, ComponentShutdown>(OnStandingShutdown);
    }

    private void OnToggleAction(Entity<SiliconStandingComponent> ent, ref ToggleSiliconRestingEvent args)
    {
        if (!CanToggleResting(ent))
            return;

        SetResting(ent, !IsResting(ent));
        args.Handled = true;
    }

    private void OnCanMove(Entity<SiliconStandingComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (IsResting(ent))
            args.Cancel();
    }

    private void OnStandingShutdown(Entity<SiliconStandingComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ToggleRestingAction);
    }

    public bool GetEffectiveResting(EntityUid uid) => IsResting(uid);

    public bool IsResting(EntityUid uid) => HasComp<SiliconRestingComponent>(uid);

    public bool CanToggleResting(EntityUid uid)
    {
        if (!HasComp<SiliconStandingComponent>(uid))
            return false;

        if (!HasComp<BorgChassisComponent>(uid))
            return false;

        if (!TryComp<BorgSwitchableSubtypeComponent>(uid, out var subtype) || subtype.BorgSubtype == null)
            return false;

        if (!_prototype.TryIndex(subtype.BorgSubtype, out var subtypePrototype))
            return false;

        return subtypePrototype.Visuals.RestBodyState != null;
    }

    public void SetResting(EntityUid uid, bool resting)
    {
        if (resting)
            EnsureComp<SiliconRestingComponent>(uid);
        else
            RemComp<SiliconRestingComponent>(uid);

        if (TryComp<AppearanceComponent>(uid, out var appearance))
            _appearance.SetData(uid, SiliconStandingVisuals.Resting, resting, appearance);

        _actionBlocker.UpdateCanMove(uid);
    }
}
