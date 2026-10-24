using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Arcane.Store.Components;
using Content.Shared.Store;
using Content.Shared.Store.Components;

namespace Content.Server.Store.Systems;

public sealed partial class StoreSystem
{
    private void ArcaneHandleUplinkRefundable(EntityUid storeUid, ListingDataWithCostModifiers listing, EntityUid product)
    {
        if (!TryComp<StoreComponent>(storeUid, out _))
            return;

        var refundComp = EnsureComp<UplinkRefundableComponent>(product);
        refundComp.OriginalUplink = storeUid;
        if (listing.Cost.Count > 0)
        {
            var (currency, amount) = listing.Cost.First();
            refundComp.Currency = currency;
            refundComp.RefundAmount = FixedPoint2.New((int) MathF.Round(amount.Float() * 0.8f));
        }

        Dirty(product, refundComp);
    }
}
