using System.Linq;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.Medical.Healing;

namespace Content.Shared.Repairable;

public sealed partial class RepairableSystem
{
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;
    [Dependency] private readonly HealingSystem _healingSystem = default!;
    [Dependency] private readonly WoundSystem _wounds = default!;

    // If there is nothign to heal on a body, dont try it.
    private bool GoobCanRepair(Entity<RepairableComponent> ent)
    {
        if (!HasComp<BodyComponent>(ent))
            return true;

        if (ent.Comp.Damage != null)
        {
            // here we create a fake healing comp
            var repairHealing = new HealingComponent
            {
                Damage = ent.Comp.Damage,
                BloodlossModifier = -100
            };

            return _healingSystem.TryGetNextDamagedPart(ent.Owner, repairHealing, out var _);
        }

        return true;
    }

    private bool GoobTryRepairIPC(Entity<RepairableComponent> ent, EntityUid user)
    {
        if (TryComp<BodyComponent>(ent.Owner, out var body) && ent.Comp.Damage != null && body != null) // repair entities with bodies
        {
            // Arcane-Edit-Start: heal like topicals, the targeted part first and the surplus over the rest of the body
            var parts = new List<EntityUid>();
            if (TryComp<TargetingComponent>(user, out var targeting))
            {
                var (partType, symmetry) = _bodySystem.ConvertTargetBodyPart(targeting.Target);
                parts.AddRange(_bodySystem.GetBodyChildrenOfType(ent, partType, body, symmetry).Select(part => part.Id).Take(1));
            }

            foreach (var part in _bodySystem.GetBodyChildren(ent, body))
            {
                if (!parts.Contains(part.Id))
                    parts.Add(part.Id);
            }

            // Welding removes all bleeding instantly. IPC don't even have blood as i'm writing this so makes 0 sense for them to have bleeds.
            var healedBleedWound = false;
            var stoppedAllBleeding = true;
            foreach (var part in parts)
            {
                if (!TryComp<WoundableComponent>(part, out var woundableComp)
                    || !_wounds.TryHealBleedingWounds(part, -100, out var modifiedBleedStopAbility, woundableComp))
                    continue;

                healedBleedWound = true;
                stoppedAllBleeding &= modifiedBleedStopAbility > 0;
            }

            if (healedBleedWound)
                _popup.PopupPredicted(stoppedAllBleeding
                        ? Loc.GetString("rebell-medical-item-stop-bleeding-fully")
                        : Loc.GetString("rebell-medical-item-stop-bleeding-partially"),
                    ent,
                    user);

            var healingLeft = ent.Comp.Damage;
            var healedTotal = new DamageSpecifier();
            foreach (var part in parts)
            {
                if (healingLeft.GetTotal() >= 0)
                    break;

                var damageChanged = _damageableSystem.ChangeDamage(part, healingLeft, true, false, origin: user);
                healedTotal -= damageChanged;
                healingLeft -= damageChanged;
            }

            _adminLogger.Add(LogType.Healed, $"{ToPrettyString(user):user} repaired {ToPrettyString(ent.Owner):target} by {healedTotal.GetTotal()}");

            // Wound severity can outlive the damage, so only remaining damage keeps the do-after going.
            return healedTotal.GetTotal() > 0
                && TryComp<DamageableComponent>(ent, out var damageable)
                && ent.Comp.Damage.DamageDict.Keys.Any(type => damageable.Damage.DamageDict.GetValueOrDefault(type) > 0);
            // Arcane-Edit-End
        }
        return false;
    }
}