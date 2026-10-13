using Content.Client.Movement.Components;
using Content.Shared._Arcane.Weapons.Aim;
using Robust.Client.Player;
using Robust.Shared.GameObjects;

namespace Content.Client._Arcane.Weapons.Aim;

public sealed class ArcaneAimSystem : SharedArcaneAimSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    private readonly Dictionary<EntityUid, EntityUid> _offsetUsers = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArcaneAimComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ArcaneAimComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnAfterState(Entity<ArcaneAimComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!ent.Comp.Active || ent.Comp.User is not { } user)
        {
            ClearAppliedOffset(ent.Owner);
            return;
        }

        if (_offsetUsers.TryGetValue(ent.Owner, out var previousUser)
            && previousUser != user)
        {
            ClearAppliedOffset(ent.Owner);
        }

        ApplyOffset(user, ent.Comp);

        if (_player.LocalEntity == user)
            _offsetUsers[ent.Owner] = user;
    }

    private void OnShutdown(Entity<ArcaneAimComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.User is { } user)
            RemoveOffset(user);
    }

    protected override void EnableAim(Entity<ArcaneAimComponent> ent, EntityUid user)
    {
        base.EnableAim(ent, user);
        ApplyOffset(user, ent.Comp);
    }

    protected override void DisableAim(Entity<ArcaneAimComponent> ent, EntityUid user)
    {
        base.DisableAim(ent, user);
        RemoveOffset(user);
    }

    private void ApplyOffset(EntityUid user, ArcaneAimComponent comp)
    {
        if (_player.LocalEntity != user)
            return;
        var offset = EnsureComp<Content.Client.Movement.Components.EyeCursorOffsetComponent>(user);
        offset.MaxOffset = comp.Offset;
        offset.OffsetSpeed = comp.OffsetSpeed;
        offset.PvsIncrease = comp.PvsIncrease;
        offset.TargetPosition = System.Numerics.Vector2.Zero;
        offset.CurrentPosition = System.Numerics.Vector2.Zero;
    }

    private void RemoveOffset(EntityUid user)
    {
        if (_player.LocalEntity != user)
            return;

        RemCompDeferred<Content.Client.Movement.Components.EyeCursorOffsetComponent>(user);
    }

    private void ClearAppliedOffset(EntityUid weapon)
    {
        if (_offsetUsers.Remove(weapon, out var user))
            RemoveOffset(user);
    }
}
