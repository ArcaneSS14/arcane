// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Orion.Economy.Systems;
using Content.Server.Popups;
using Content.Server.Salvage.JobBoard;
using Content.Server.Station.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Cargo.Systems;

public sealed class PriceGunSystem : SharedPriceGunSystem
{
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly PricingSystem _pricingSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly CargoSystem _bountySystem = default!;
    [Dependency] private readonly SalvageJobBoardSystem _salvageJobBoard = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StationSystem _station = default!; // Orion
    [Dependency] private readonly MarketSystem _market = default!; // Orion

    protected override bool GetPriceOrBounty(Entity<PriceGunComponent> entity, EntityUid target, EntityUid user)
    {
        if (!TryComp(entity.Owner, out UseDelayComponent? useDelay) || _useDelay.IsDelayed((entity.Owner, useDelay)))
            return false;
        // Check if we're scanning a bounty crate
        if (_bountySystem.IsBountyComplete(target, out _))
        {
            _popupSystem.PopupEntity(Loc.GetString("price-gun-bounty-complete"), user, user);
        }
        else if (_salvageJobBoard.FulfillsSalvageJob(target, null, out _))
        {
            _popupSystem.PopupEntity(Loc.GetString("price-gun-salvjob-complete"), user, user);
        }
        else // Otherwise appraise the price
        {
            var price = _pricingSystem.GetPrice(target);

            // Orion-Start
            if (_station.GetOwningStation(user) is { } station)
                price = _market.AdjustSellPrice(station, target, price);
            // Orion-End

            _popupSystem.PopupEntity(Loc.GetString("price-gun-pricing-result",
                    ("object", Identity.Entity(target, EntityManager)),
                    ("price", $"{price:F2}")),
                user,
                user);
        }

        _audio.PlayPvs(entity.Comp.AppraisalSound, entity.Owner);
        _useDelay.TryResetDelay((entity.Owner, useDelay));
        return true;
    }
}
