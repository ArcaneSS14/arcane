using Content.Shared._Arcane.ERP;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class CumOverlayRequirement : InvertableErpRequirement
{
    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        var hasOverlay = entityManager.HasComponent<CumOverlayComponent>(uid);

        return Inverted ? !hasOverlay : hasOverlay;
    }
}
