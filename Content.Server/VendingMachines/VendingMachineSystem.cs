// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Server._Orion.Economy.Components;
using Content.Server._Orion.Economy.Systems;
using Content.Server.Access.Systems;
using Content.Server.Cargo.Systems;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Shared.Cargo;
using Content.Shared._Orion.VendingMachines.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Server.Vocalization.Systems;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Emp;
using Content.Shared.Power;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Throwing;
using Content.Shared.UserInterface;
using Content.Shared.VendingMachines;
using Content.Shared.Wall;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.VendingMachines
{
    public sealed class VendingMachineSystem : SharedVendingMachineSystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly PricingSystem _pricing = default!;
        [Dependency] private readonly ThrowingSystem _throwingSystem = default!;
        // Orion-Edit-Start
        [Dependency] private readonly BankSystem _bank = default!;
        [Dependency] private readonly CargoSystem _cargo = default!;
        [Dependency] private readonly IdCardSystem _idCard = default!;
        [Dependency] private readonly StationSystem _station = default!;
        [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
        [Dependency] private readonly UserInterfaceSystem _ui = default!;
        // Orion-Edit-End

        private const float WallVendEjectDistanceFromWall = 1f;
        private const float DefaultDepartmentDiscount = 0.2f; // Orion
        private static readonly ProtoId<CargoAccountPrototype> CargoDepartmentAccount = "Cargo"; // Orion

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<VendingMachineComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<VendingMachineComponent, DamageChangedEvent>(OnDamageChanged);
            SubscribeLocalEvent<VendingMachineComponent, PriceCalculationEvent>(OnVendingPrice);
            SubscribeLocalEvent<VendingMachineComponent, TryVocalizeEvent>(OnTryVocalize);

            SubscribeLocalEvent<VendingMachineComponent, VendingMachineSelfDispenseEvent>(OnSelfDispense);

            SubscribeLocalEvent<VendingMachineRestockComponent, PriceCalculationEvent>(OnPriceCalculation);
            // Orion-Edit-Start
            SubscribeLocalEvent<VendingMachineComponent, VendingMachineBeforeEjectEvent>(OnBeforeEject);
            SubscribeLocalEvent<VendingMachineComponent, BoundUIOpenedEvent>(OnBoundUiOpened);
            // Orion-Edit-End
        }

        // Orion-Edit-Start
        private void OnBeforeEject(Entity<VendingMachineComponent> ent, ref VendingMachineBeforeEjectEvent args)
        {
            if (args.User is not { } user)
                return;

            args.Price = GetFinalPrice(ent, user, args.InventoryType, args.Price);

            if (args.Price <= 0)
            {
                SendInventoryUpdate(ent, user);
                return;
            }

            if (!_idCard.TryFindIdCard(user, out var idCard) || string.IsNullOrWhiteSpace(idCard.Comp.BankAccountId))
            {
                if (TryBorgPay(ent, user, args.Price, args.ItemId))
                {
                    SendInventoryUpdate(ent, user);
                    return;
                }

                Popup.PopupClient(Loc.GetString("vending-machine-purchase-no-id"), ent, user);
                args.Cancelled = true;
                return;
            }

            if (!_bank.TryFindAccountById(idCard.Comp.BankAccountId, out var account))
            {
                Popup.PopupClient(Loc.GetString("vending-machine-purchase-no-account"), ent, user);
                args.Cancelled = true;
                return;
            }

            if (account.Comp.Balance < args.Price)
            {
                Popup.PopupClient(Loc.GetString("vending-machine-purchase-insufficient-funds"), ent, user);
                args.Cancelled = true;
                return;
            }

            if (_station.GetOwningStation(ent.Owner) is not { } station ||
                !TryComp<StationBankAccountComponent>(station, out var bankAcc))
            {
                Popup.PopupClient(Loc.GetString("vending-machine-purchase-payment-failed"), ent, user);
                args.Cancelled = true;
                return;
            }

            var department = TryComp<VendingMachinePricingComponent>(ent.Owner, out var pricing) &&
                pricing.DepartmentAccount is { } configuredDepartment
                    ? configuredDepartment
                    : bankAcc.PrimaryAccount;

            if (!_cargo.HasAccount((station, bankAcc), department))
            {
                Popup.PopupClient(Loc.GetString("vending-machine-purchase-payment-failed"), ent, user);
                args.Cancelled = true;
                return;
            }

            var reasonData = $"{args.ItemId}|{department.Id}";
            if (!_bank.Withdraw(account, args.Price, "vending-purchase", reasonData: reasonData))
            {
                Popup.PopupClient(Loc.GetString("vending-machine-purchase-payment-failed"), ent, user);
                args.Cancelled = true;
                return;
            }

            _cargo.UpdateBankAccount((station, bankAcc), args.Price, department);
            SendInventoryUpdate(ent, user);
        }

        private void OnBoundUiOpened(Entity<VendingMachineComponent> ent, ref BoundUIOpenedEvent args)
        {
            SendInventoryUpdate(ent, args.Actor);
        }

        private int GetFinalPrice(Entity<VendingMachineComponent> ent, EntityUid? user, InventoryType type, int basePrice)
        {
            if (basePrice <= 0)
                return 0;

            if (!TryComp<VendingMachinePricingComponent>(ent.Owner, out var pricing))
                return basePrice;

            if (pricing.AllProductsFree)
                return 0;

            if (type != InventoryType.Regular ||
                pricing.DiscountDepartment is not { } discountDepartment ||
                user == null ||
                !TryGetAccount(user.Value, out var account) ||
                !_bank.TryGetDepartment(account, out var buyerDepartment) ||
                buyerDepartment != discountDepartment)
            {
                return basePrice;
            }

            var discount = pricing.DepartmentDiscount ?? DefaultDepartmentDiscount;
            return Math.Max((int) Math.Round(basePrice * discount, MidpointRounding.AwayFromZero), 1);
        }

        private bool TryGetAccount(EntityUid user, out Entity<StationAccountComponent> account)
        {
            account = default;
            if (!_idCard.TryFindIdCard(user, out var idCard) || string.IsNullOrWhiteSpace(idCard.Comp.BankAccountId))
                return false;

            return _bank.TryFindAccountById(idCard.Comp.BankAccountId, out account);
        }

        private void SendInventoryUpdate(Entity<VendingMachineComponent> ent, EntityUid user)
        {
            var inventory = GetAllInventory(ent.Owner, ent.Comp);
            for (var i = 0; i < inventory.Count; i++)
            {
                var entry = new VendingMachineInventoryEntry(inventory[i]);
                entry.DisplayPrice = GetFinalPrice(ent, user, entry.Type, entry.Price);
                inventory[i] = entry;
            }

            int? balance = TryGetAccount(user, out var account)
                ? account.Comp.Balance
                : null;

            _ui.ServerSendUiMessage(
                ent.Owner,
                VendingMachineUiKey.Key,
                new VendingMachineInventoryUpdateMessage(inventory, balance),
                user);
        }

        private bool TryBorgPay(Entity<VendingMachineComponent> ent, EntityUid user, int price, string itemId)
        {
            if (!HasComp<BorgChassisComponent>(user))
                return false;

            if (_station.GetOwningStation(ent.Owner) is not { } station ||
                !TryComp<StationBankAccountComponent>(station, out var bankAcc))
            {
                return false;
            }

            var cargoBalance = _cargo.GetBalanceFromAccount((station, bankAcc), CargoDepartmentAccount);
            if (cargoBalance < price)
            {
                Popup.PopupClient(Loc.GetString("vending-machine-purchase-insufficient-funds"), ent, user);
                return false;
            }

            _cargo.UpdateBankAccount((station, bankAcc), -price, CargoDepartmentAccount);
            _adminLogger.Add(
                LogType.Action,
                LogImpact.Low,
                $"Borg purchase charged cargo account. Item: {itemId}. Amount: {price}. Station: {station}.");
            return true;
        }
        // Orion-Edit-End

        private void OnVendingPrice(EntityUid uid, VendingMachineComponent component, ref PriceCalculationEvent args)
        {
            var price = 0.0;

            foreach (var entry in component.Inventory.Values)
            {
                if (!PrototypeManager.TryIndex<EntityPrototype>(entry.ID, out var proto))
                {
                    Log.Error($"Unable to find entity prototype {entry.ID} on {ToPrettyString(uid)} vending.");
                    continue;
                }

                price += entry.Amount * _pricing.GetEstimatedPrice(proto);
            }

            args.Price += price;
        }

        protected override void OnMapInit(EntityUid uid, VendingMachineComponent component, MapInitEvent args)
        {
            base.OnMapInit(uid, component, args);

            InitializeOffStationFreePricing(uid); // Orion

            if (HasComp<ApcPowerReceiverComponent>(uid))
            {
                TryUpdateVisualState((uid, component));
            }
        }

        // Orion-Edit-Start
        private void InitializeOffStationFreePricing(EntityUid uid)
        {
            if (!TryComp<VendingMachinePricingComponent>(uid, out var pricing))
                return;

            if (pricing.AllProductsFreeOverride is { } allProductsFreeOverride)
            {
                pricing.AllProductsFree = allProductsFreeOverride;
                Dirty(uid, pricing);
                return;
            }

            if (_station.GetOwningStation(uid) != null)
                return;

            pricing.AllProductsFree = true;
            Dirty(uid, pricing);
        }
        // Orion-Edit-End

        private void OnPowerChanged(EntityUid uid, VendingMachineComponent component, ref PowerChangedEvent args)
        {
            TryUpdateVisualState((uid, component));
        }

        private void OnDamageChanged(EntityUid uid, VendingMachineComponent component, DamageChangedEvent args)
        {
            if (!args.DamageIncreased && component.Broken)
            {
                component.Broken = false;
                Dirty(uid, component);
                TryUpdateVisualState((uid, component));
                return;
            }

            if (component.Broken || component.DispenseOnHitCoolingDown ||
                component.DispenseOnHitChance == null || args.DamageDelta == null)
                return;

            if (args.DamageIncreased && args.DamageDelta.GetTotal() >= component.DispenseOnHitThreshold &&
                _random.Prob(component.DispenseOnHitChance.Value))
            {
                if (component.DispenseOnHitCooldown != null)
                {
                    component.DispenseOnHitEnd = Timing.CurTime + component.DispenseOnHitCooldown.Value;
                }

                EjectRandom(uid, throwItem: true, forceEject: true, component);
            }
        }

        private void OnSelfDispense(EntityUid uid, VendingMachineComponent component, VendingMachineSelfDispenseEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = true;
            EjectRandom(uid, throwItem: true, forceEject: false, component);
        }

        /// <summary>
        /// Sets the <see cref="VendingMachineComponent.CanShoot"/> property of the vending machine.
        /// </summary>
        public void SetShooting(EntityUid uid, bool canShoot, VendingMachineComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            component.CanShoot = canShoot;
        }

        /// <summary>
        /// Sets the <see cref="VendingMachineComponent.Contraband"/> property of the vending machine.
        /// </summary>
        public void SetContraband(EntityUid uid, bool contraband, VendingMachineComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            component.Contraband = contraband;
            Dirty(uid, component);
        }

        /// <summary>
        /// Ejects a random item from the available stock. Will do nothing if the vending machine is empty.
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="throwItem">Whether to throw the item in a random direction after dispensing it.</param>
        /// <param name="forceEject">Whether to skip the regular ejection checks and immediately dispense the item without animation.</param>
        /// <param name="vendComponent"></param>
        public void EjectRandom(EntityUid uid, bool throwItem, bool forceEject = false, VendingMachineComponent? vendComponent = null)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            var availableItems = GetAvailableInventory(uid, vendComponent);
            if (availableItems.Count <= 0)
                return;

            var item = _random.Pick(availableItems);

            if (forceEject)
            {
                vendComponent.NextItemToEject = item.ID;
                vendComponent.ThrowNextItem = throwItem;
                var entry = GetEntry(uid, item.ID, item.Type, vendComponent);
                if (entry != null)
                    entry.Amount--;
                EjectItem(uid, vendComponent, forceEject);
            }
            else
            {
                TryEjectVendorItem(uid, item.Type, item.ID, throwItem, user: null, vendComponent: vendComponent);
            }
        }

        protected override void EjectItem(EntityUid uid, VendingMachineComponent? vendComponent = null, bool forceEject = false)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            // No need to update the visual state because we never changed it during a forced eject
            if (!forceEject)
                TryUpdateVisualState((uid, vendComponent));

            if (string.IsNullOrEmpty(vendComponent.NextItemToEject))
            {
                vendComponent.ThrowNextItem = false;
                return;
            }

            // Default spawn coordinates
            var xform = Transform(uid);
            var spawnCoordinates = xform.Coordinates;

            //Make sure the wallvends spawn outside of the wall.
            if (TryComp<WallMountComponent>(uid, out var wallMountComponent))
            {
                var offset = (wallMountComponent.Direction + xform.LocalRotation - Math.PI / 2).ToVec() * WallVendEjectDistanceFromWall;
                spawnCoordinates = spawnCoordinates.Offset(offset);
            }

            var ent = Spawn(vendComponent.NextItemToEject, spawnCoordinates);

            if (vendComponent.ThrowNextItem)
            {
                var range = vendComponent.NonLimitedEjectRange;
                var direction = new Vector2(_random.NextFloat(-range, range), _random.NextFloat(-range, range));
                _throwingSystem.TryThrow(ent, direction, vendComponent.NonLimitedEjectForce);
            }

            vendComponent.NextItemToEject = null;
            vendComponent.ThrowNextItem = false;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var disabled = EntityQueryEnumerator<EmpDisabledComponent, VendingMachineComponent>();
            while (disabled.MoveNext(out var uid, out _, out var comp))
            {
                if (comp.NextEmpEject < Timing.CurTime)
                {
                    EjectRandom(uid, true, false, comp);
                    comp.NextEmpEject += (5 * comp.EjectDelay);
                }
            }
        }

        private void OnPriceCalculation(EntityUid uid, VendingMachineRestockComponent component, ref PriceCalculationEvent args)
        {
            List<double> priceSets = new();

            // Find the most expensive inventory and use that as the highest price.
            foreach (var vendingInventory in component.CanRestock)
            {
                double total = 0;

                if (PrototypeManager.TryIndex(vendingInventory, out VendingMachineInventoryPrototype? inventoryPrototype))
                {
                    foreach (var (item, amount) in inventoryPrototype.StartingInventory)
                    {
                        if (PrototypeManager.TryIndex(item, out EntityPrototype? entity))
                            total += _pricing.GetEstimatedPrice(entity) * amount;
                    }
                }

                priceSets.Add(total);
            }

            args.Price += priceSets.Max();
        }

        private void OnTryVocalize(Entity<VendingMachineComponent> ent, ref TryVocalizeEvent args)
        {
            args.Cancelled |= ent.Comp.Broken;
        }
    }
}
