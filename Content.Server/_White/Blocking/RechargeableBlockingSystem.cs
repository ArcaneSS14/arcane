// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;

namespace Content.Server._White.Blocking;

public sealed class RechargeableBlockingSystem : EntitySystem
{
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RechargeableBlockingComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<RechargeableBlockingComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<RechargeableBlockingComponent, ItemToggleActivateAttemptEvent>(AttemptToggle);
        SubscribeLocalEvent<RechargeableBlockingComponent, ChargeChangedEvent>(OnChargeChanged);
        SubscribeLocalEvent<RechargeableBlockingComponent, PowerCellChangedEvent>(OnPowerCellChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RechargeableBlockingComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (HasComp<EmpDisabledComponent>(uid))
            {
                if (_battery.TryGetBatteryComponent(uid, out var empBattery, out var empBatteryUid))
                    _battery.SetCharge((empBatteryUid.Value, empBattery), 0f);

                if (!component.Discharged)
                {
                    component.Discharged = true;
                    _itemToggle.TryDeactivate(uid, predicted: false);
                }
                continue;
            }

            if (!component.Discharged)
                continue;

            CheckCharge(uid, component);
        }
    }

    private void OnExamined(Entity<RechargeableBlockingComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Discharged)
            return;

        if (HasComp<EmpDisabledComponent>(ent))
        {
            args.PushMarkup(Loc.GetString("rechargeable-blocking-emp-disabled"));
            return;
        }

        args.PushMarkup(Loc.GetString("rechargeable-blocking-discharged"));
        args.PushMarkup(Loc.GetString("rechargeable-blocking-remaining-time", ("remainingTime", GetRemainingTime(ent))));
    }

    private int GetRemainingTime(EntityUid uid)
    {
        if (!_battery.TryGetBatteryComponent(uid, out var batteryComponent, out var batteryUid)
            || !TryComp<BatterySelfRechargerComponent>(batteryUid, out var recharger)
            || recharger is not { AutoRechargeRate: > 0 })
            return 0;

        var charge = _battery.GetCharge((batteryUid.Value, batteryComponent));
        var remaining = MathF.Max(0, batteryComponent.MaxCharge - charge);
        return (int) MathF.Ceiling(remaining / recharger.AutoRechargeRate);
    }

    private void OnDamageChanged(EntityUid uid, RechargeableBlockingComponent component, DamageChangedEvent args)
    {
        if (!_battery.TryGetBatteryComponent(uid, out var batteryComponent, out var batteryUid)
            || !_itemToggle.IsActivated(uid)
            || args.DamageDelta == null)
            return;

        var batteryUse = Math.Min(args.DamageDelta.GetTotal().Float(), _battery.GetCharge((batteryUid.Value, batteryComponent)));
        _battery.TryUseCharge(batteryUid.Value, batteryUse);
    }

    private void AttemptToggle(EntityUid uid, RechargeableBlockingComponent component, ref ItemToggleActivateAttemptEvent args)
    {
        if (HasComp<EmpDisabledComponent>(uid))
        {
            args.Cancelled = true;
            return;
        }

        if (!_battery.TryGetBatteryComponent(uid, out var battery, out var batteryUid))
            return;

        var charge = _battery.GetCharge((batteryUid.Value, battery));
        if (component.Discharged && charge < battery.MaxCharge)
        {
            if (HasComp<BatterySelfRechargerComponent>(batteryUid))
                args.Popup = Loc.GetString("rechargeable-blocking-remaining-time-popup", ("remainingTime", GetRemainingTime(uid)));
            else
                args.Popup = Loc.GetString("rechargeable-blocking-not-enough-charge-popup");

            args.Cancelled = true;
        }
    }

    private void OnChargeChanged(EntityUid uid, RechargeableBlockingComponent component, ChargeChangedEvent args)
    {
        CheckCharge(uid, component);
    }

    private void OnPowerCellChanged(EntityUid uid, RechargeableBlockingComponent component, PowerCellChangedEvent args)
    {
        CheckCharge(uid, component);
    }

    private void CheckCharge(EntityUid uid, RechargeableBlockingComponent component)
    {
        if (!_battery.TryGetBatteryComponent(uid, out var battery, out var batteryUid))
            return;

        var charge = _battery.GetCharge((batteryUid.Value, battery));

        if (charge < 1)
        {
            if (!component.Discharged)
            {
                if (TryComp(batteryUid, out BatterySelfRechargerComponent? recharger)
                    && recharger.AutoRechargeRate != component.DischargedRechargeRate)
                {
                    recharger.AutoRechargeRate = component.DischargedRechargeRate;
                    Dirty(batteryUid.Value, recharger);
                    _battery.RefreshChargeRate((batteryUid.Value, battery));
                }

                component.Discharged = true;
                _itemToggle.TryDeactivate(uid, predicted: false);
            }
            return;
        }

        if (charge < battery.MaxCharge)
            return;

        if (component.Discharged)
        {
            component.Discharged = false;

            if (TryComp(batteryUid, out BatterySelfRechargerComponent? recharger2)
                && recharger2.AutoRechargeRate != component.ChargedRechargeRate)
            {
                recharger2.AutoRechargeRate = component.ChargedRechargeRate;
                Dirty(batteryUid.Value, recharger2);
                _battery.RefreshChargeRate((batteryUid.Value, battery));
            }
        }
    }
}
