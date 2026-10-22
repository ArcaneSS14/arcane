using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
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

            if (_healingSystem.TryGetNextDamagedPart(ent.Owner, repairHealing, out var _))
                return true;

            if (TryComp<DamageableComponent>(ent.Owner, out var damageable) && damageable.TotalDamage > 0)
                return true;

            return false;
        }

        if (ent.Comp.DamageValue != null && TryComp<DamageableComponent>(ent.Owner, out var dmg) && dmg.TotalDamage > 0)
            return true;

        return true;
    }

    private bool GoobTryRepairIPC(Entity<RepairableComponent> ent, EntityUid user)
    {
        if (!TryComp<BodyComponent>(ent.Owner, out var body))
            return false;

        if (ent.Comp.Damage == null)
        {
            if (ent.Comp.DamageValue != null && TryComp<DamageableComponent>(ent.Owner, out var dmgValue))
            {
                var changed = _damageableSystem.HealEvenly(ent.Owner, ent.Comp.DamageValue.Value, origin: user);
                _adminLogger.Add(LogType.Healed, $"{ToPrettyString(user):user} repaired {ToPrettyString(ent.Owner):target} by {changed.GetTotal()}");
                return dmgValue.TotalDamage > 0;
            }
            return false;
        }

        // here we create a fake healing comp
        var repairHealing = new HealingComponent
        {
            Damage = ent.Comp.Damage,
            BloodlossModifier = -100
        };

        var targetedWoundable = EntityUid.Invalid;
        if (TryComp<TargetingComponent>(user, out var targeting))
        {
            var (partType, symmetry) = _bodySystem.ConvertTargetBodyPart(targeting.Target);
            var targetedBodyPart = _bodySystem.GetBodyChildrenOfType(ent, partType, body, symmetry).ToList().FirstOrDefault();
            targetedWoundable = targetedBodyPart.Id;
        }
        else
        {
            if (_healingSystem.TryGetNextDamagedPart(ent, repairHealing, out var limbTemp) && limbTemp is not null)
                targetedWoundable = limbTemp.Value;
        }

        if (targetedWoundable == EntityUid.Invalid || !TryComp<DamageableComponent>(targetedWoundable, out _))
        {
            if (TryComp<DamageableComponent>(ent.Owner, out var dmg))
            {
                _damageableSystem.ChangeDamage(ent.Owner, ent.Comp.Damage, true, false, origin: user);
                _adminLogger.Add(LogType.Healed, $"{ToPrettyString(user):user} repaired {ToPrettyString(ent.Owner):target} (fallback)");
                return dmg.TotalDamage > 0;
            }
            return false;
        }

        if (!_healingSystem.IsBodyDamaged((ent.Owner, body), null, repairHealing, targetedWoundable))                    // Check if there is anything to heal on the initial limb target
        {
            if (_healingSystem.TryGetNextDamagedPart(ent, repairHealing, out var limbTemp) && limbTemp is not null)      // If not then get the next limb to heal
                targetedWoundable = limbTemp.Value;
        }

        // Welding removes all bleeding instantly. IPC don't even have blood as i'm writing this so makes 0 sense for them to have bleeds.
        if (TryComp<WoundableComponent>(targetedWoundable, out var woundableComp))
        {
            bool healedBleedWound = false;
            FixedPoint2 modifiedBleedStopAbility = 0;
            healedBleedWound = _wounds.TryHealBleedingWounds(targetedWoundable, repairHealing.BloodlossModifier, out modifiedBleedStopAbility, woundableComp);
            if (healedBleedWound)
                _popup.PopupPredicted(modifiedBleedStopAbility > 0
                        ? Loc.GetString("rebell-medical-item-stop-bleeding-fully")
                        : Loc.GetString("rebell-medical-item-stop-bleeding-partially"),
                    ent,
                    user);
        }

        var damageChanged = _damageableSystem.ChangeDamage(targetedWoundable, ent.Comp.Damage, true, false, origin: user);
        _adminLogger.Add(LogType.Healed, $"{ToPrettyString(user):user} repaired {ToPrettyString(ent.Owner):target} by {damageChanged?.GetTotal()}");

        if (_healingSystem.TryGetNextDamagedPart(ent.Owner, repairHealing, out var _))
            return true;

        return TryComp<DamageableComponent>(ent.Owner, out var remaining) && remaining.TotalDamage > 0;
    }
}
