// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Religion;
using Content.Server.Polymorph.Components;
using Content.Shared._Shitcode.Heretic.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Coordinates;
using Content.Shared.Heretic;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Server.Heretic.Abilities;

public sealed partial class HereticAbilitySystem
{
    private static readonly EntProtoId<VoidAscensionAuraComponent> VoidAuraId = "VoidAscensionAura";

    protected override void SubscribeVoid()
    {
        base.SubscribeVoid();

        SubscribeLocalEvent<HereticAscensionVoidEvent>(OnAscensionVoid);

        SubscribeLocalEvent<HereticVoidPrisonEvent>(OnVoidPrison);

        SubscribeLocalEvent<VoidPrisonComponent, PolymorphedEvent>(OnPrisonRevert);
    }

    private void OnPrisonRevert(Entity<VoidPrisonComponent> ent, ref PolymorphedEvent args)
    {
        if (!args.IsRevert)
            return;

        Spawn(ent.Comp.EndEffect, Transform(ent).Coordinates);
        Voidcurse.DoCurse(args.NewEntity);
    }

    private void OnAscensionVoid(HereticAscensionVoidEvent args)
    {
        if (!args.Negative)
            SpawnAttachedTo(VoidAuraId, args.Heretic.ToCoordinates());
        else
        {
            var childEnumerator = Transform(args.Heretic).ChildEnumerator;
            while (childEnumerator.MoveNext(out var child))
            {
                if (HasComp<VoidAscensionAuraComponent>(child))
                    QueueDel(child);
            }
        }
    }

    private void OnVoidPrison(HereticVoidPrisonEvent args)
    {
        var target = args.Target;

        if (!HasComp<PolymorphableComponent>(target) || HasComp<VoidPrisonComponent>(target))
            return;

        if (!TryUseAbility(args))
            return;

        args.Handled = true;

        var ev = new BeforeCastTouchSpellEvent(target);
        RaiseLocalEvent(target, ev, true);
        if (ev.Cancelled)
            return;

        _poly.PolymorphEntity(target, args.Polymorph);
    }
}
