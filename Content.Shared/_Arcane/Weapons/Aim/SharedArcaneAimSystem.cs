using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Shared._Arcane.Weapons.Aim;

public abstract class SharedArcaneAimSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

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

        if (!IsHeldBy(user, ent.Owner) ||
            !TryComp<WieldableComponent>(ent.Owner, out var wieldable) ||
            !wieldable.Wielded)
        {
            return;
        }

        EnableAim(ent, user);
    }

    private bool IsHeldBy(EntityUid user, EntityUid item)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return false;

        foreach (var handId in hands.SortedHands)
        {
            if (_hands.TryGetHeldItem((user, hands), handId, out var held) &&
                held == item)
            {
                return true;
            }
        }

        return false;
    }

    protected virtual void EnableAim(Entity<ArcaneAimComponent> ent, EntityUid user)
    {
        ent.Comp.Active = true;
        ent.Comp.User = user;
        Dirty(ent);

        var camera = EnsureComp<ArcaneAimCameraOffsetComponent>(user);
        camera.Offset = ent.Comp.Offset;
        camera.Source = ent.Owner;
        Dirty(user, camera);
    }

    protected virtual void DisableAim(Entity<ArcaneAimComponent> ent, EntityUid user)
    {
        ent.Comp.Active = false;
        ent.Comp.User = null;
        Dirty(ent);

        if (TryComp<ArcaneAimCameraOffsetComponent>(user, out var camera) &&
            camera.Source == ent.Owner)
        {
            RemComp<ArcaneAimCameraOffsetComponent>(user);
        }
    }

    private void OnActorMoveInput(Entity<ActorComponent> ent, ref MoveInputEvent args)
    {
        if (!args.State || !TryComp<HandsComponent>(ent.Owner, out var hands))
            return;

        foreach (var handId in hands.SortedHands)
        {
            if (!_hands.TryGetHeldItem((ent.Owner, hands), handId, out var item) ||
                item is not { } itemUid ||
                !TryComp<ArcaneAimComponent>(itemUid, out var aim))
            {
                continue;
            }

            if (aim.Active && aim.User == ent.Owner)
                DisableAim(new Entity<ArcaneAimComponent>(itemUid, aim), ent.Owner);
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
}
