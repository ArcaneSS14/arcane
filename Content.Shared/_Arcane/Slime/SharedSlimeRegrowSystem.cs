// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Shared._Arcane.Slime;

/// <summary>
/// Handles granting and removing the slime limb regrow action.
/// </summary>
public abstract partial class SharedSlimeRegrowSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlimeRegrowComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SlimeRegrowComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<SlimeRegrowComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.ActionEnt = _actions.AddAction(ent, ent.Comp.ActionId);
        Dirty(ent, ent.Comp);
    }

    private void OnShutdown(Entity<SlimeRegrowComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEnt);
    }
}