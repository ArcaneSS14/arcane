using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Arcane.Store.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.Containers;

namespace Content.Server.Store.Systems;

public sealed partial class StoreSystem
{
    private const float RefundRatio = 0.8f;

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
            refundComp.RefundAmount = FixedPoint2.New((int) MathF.Floor(amount.Float() * RefundRatio));
        }

        if (TryComp<ContainerManagerComponent>(product, out var containerManager))
        {
            var prototypes = new List<string>();
            foreach (var container in containerManager.Containers.Values)
            {
                foreach (var entity in container.ContainedEntities)
                {
                    var meta = MetaData(entity);
                    if (meta.EntityPrototype != null)
                        prototypes.Add(meta.EntityPrototype.ID);
                }
            }

            if (prototypes.Count > 0)
                refundComp.OriginalContainerPrototypes = prototypes;
        }

        if (TryComp<SolutionContainerManagerComponent>(product, out var solutionManager) &&
            solutionManager.Solutions != null)
        {
            var totalVolume = 0f;
            foreach (var solution in solutionManager.Solutions.Values)
            {
                totalVolume += solution.Volume.Float();
            }
            refundComp.OriginalSolutionVolume = totalVolume;
        }
    }
}
