using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Movement.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Shared._Arcane.Weapons.Aim;

public abstract class SharedArcaneAimSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArcaneAimComponent, ArcaneAimActionEvent>(OnAction);
        SubscribeLocalEvent<ArcaneAimComponent, GotUnequippedHandEvent>(OnGotUnequippedHand);
        SubscribeLocalEvent<ArcaneAimComponent, EntityTerminatingEvent>(OnItemTerminating);
        SubscribeLocalEvent<ActorComponent, MoveInputEvent>(OnActorMoveInput);
        SubscribeLocalEvent<ArcaneAimComponent, ItemUnwieldedEvent>(OnItemUnwielded);
    }

    private void OnAction(Entity<ArcaneAimComponent> ent, ref ArcaneAimActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var user = args.Performer;

        if (ent.Comp.Active)
        {
            DisableAim(ent, user);
            return;
        }
        if (!IsWielded(ent))
            return;

        EnableAim(ent, user);
    }

    protected virtual void EnableAim(Entity<ArcaneAimComponent> ent, EntityUid user)
    {
        ent.Comp.Active = true;
        ent.Comp.User = user;
        Dirty(ent);
    }

    protected virtual void DisableAim(Entity<ArcaneAimComponent> ent, EntityUid user)
    {
        ent.Comp.Active = false;
        ent.Comp.User = null;
        Dirty(ent);
    }

    private void OnActorMoveInput(Entity<ActorComponent> ent, ref MoveInputEvent args)
    {
        if (!args.State)
            return;

        var query = EntityQueryEnumerator<ArcaneAimComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Active && comp.User == ent.Owner)
                DisableAim((uid, comp), ent.Owner);
        }
    }

    private void OnGotUnequippedHand(Entity<ArcaneAimComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (ent.Comp.Active && ent.Comp.User == args.User)
            DisableAim(ent, args.User);
    }

    private void OnItemTerminating(Entity<ArcaneAimComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Active && ent.Comp.User is { } user)
            DisableAim(ent, user);
    }

    private void OnItemUnwielded(Entity<ArcaneAimComponent> ent, ref ItemUnwieldedEvent args)
    {
        if (ent.Comp.Active && ent.Comp.User is { } user)
            DisableAim(ent, user);
    }

    private bool IsWielded(EntityUid uid)
    {
        if (!TryComp<WieldableComponent>(uid, out var wieldable))
            return true;

        return wieldable.Wielded;
    }
}
