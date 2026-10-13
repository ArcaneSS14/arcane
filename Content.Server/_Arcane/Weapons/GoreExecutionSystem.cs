using System.Linq;
using Content.Shared._Arcane.Weapons.GoreExecution;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Administration.Managers;

namespace Content.Server._Arcane.Weapons;

public sealed class GoreExecutionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly WoundSystem _wounds = default!;
    [Dependency] private readonly ISharedAdminManager _admin = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GoreExecutionComponent, ToggleActionEvent>(OnToggleAction);
        SubscribeLocalEvent<GoreExecutionComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<GoreExecutionComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<GoreExecutionBulletComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnToggleAction(Entity<GoreExecutionComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!_admin.IsAdmin(args.Performer))
        {
            _popup.PopupEntity(
                Loc.GetString("arcane-gore-execution-no-access"),
                ent.Owner,
                args.Performer);
            return;
        }

        args.Toggle = true;
        ent.Comp.ActionEntity = args.Action.Owner;

        SetActive(ent, !ent.Comp.Active, args.Performer);
    }

    private void OnAmmoShot(Entity<GoreExecutionComponent> ent, ref AmmoShotEvent args)
    {
        if (!ent.Comp.Active)
            return;

        foreach (var projectile in args.FiredProjectiles)
        {
            EnsureComp<GoreExecutionBulletComponent>(projectile);
        }

        var holder = TryComp<GunComponent>(ent.Owner, out var gun) ? gun.Holder : null;
        SetActive(ent, false, holder);
    }

    private void OnUnequipped(Entity<GoreExecutionComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (ent.Comp.Active)
            SetActive(ent, false, null);
    }

    private void SetActive(Entity<GoreExecutionComponent> ent, bool active, EntityUid? user)
    {
        ent.Comp.Active = active;

        if (ent.Comp.ActionEntity is { } action && !TerminatingOrDeleted(action))
            _actions.SetToggled(action, active);

        if (user is not { } performer)
            return;

        var key = active ? "arcane-gore-execution-on" : "arcane-gore-execution-off";
        _popup.PopupEntity(Loc.GetString(key), ent.Owner, performer);
    }

    private void OnProjectileHit(Entity<GoreExecutionBulletComponent> ent, ref ProjectileHitEvent args)
    {
        if (!_mobState.IsAlive(args.Target))
            return;

        if (!TryComp<BodyComponent>(args.Target, out var body))
            return;

        var head = _body.GetBodyChildrenOfType(args.Target, BodyPartType.Head, body).FirstOrDefault();
        var chest = _body.GetBodyChildrenOfType(args.Target, BodyPartType.Chest, body).FirstOrDefault();

        if (head.Id == default || chest.Id == default)
            return;

        foreach (var organ in _body.GetPartOrgans(head.Id, head.Component).ToArray())
        {
            _body.RemoveOrgan(organ.Id, organ.Component);
        }

        if (HasComp<WoundableComponent>(head.Id))
            _wounds.AmputateWoundable(chest.Id, head.Id);
        else
            _body.TryDetachPart(head.Id, head.Component);

        QueueDel(head.Id);

        _mobState.ChangeMobState(args.Target, MobState.Dead);
    }
}
