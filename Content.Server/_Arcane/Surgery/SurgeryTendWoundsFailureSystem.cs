// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Arcane.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Effects.Step;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Surgery;

public sealed partial class SurgeryTendWoundsFailureSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedSurgerySystem _surgery = default!;

    [SubscribeLocalEvent]
    private void OnBotched(Entity<SurgeryTendWoundsEffectComponent> ent, ref SurgeryStepBotchedEvent args)
    {
        var damage = ent.Comp.Damage * -ent.Comp.FailureFlatMultiplier;
        foreach (var group in ent.Comp.AdditionalGroups.Prepend(ent.Comp.MainGroup))
        {
            var bonus = _surgery.GetTendableDamage(args.Part, group) * ent.Comp.HealMultiplier * ent.Comp.FailureSeverityMultiplier;
            damage += new DamageSpecifier(_prototypes.Index(group), bonus);
        }

        foreach (var type in damage.DamageDict.Keys)
            damage.WoundSeverityMultipliers[type] = 0;

        _damageable.TryChangeDamage(args.Part, damage, ignoreResistances: true, origin: args.User);
    }
}
