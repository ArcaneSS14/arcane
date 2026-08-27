using Content.Shared._Arcane.ERP;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class CumOverlayRequirement : ErpRequirement
{
    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        if (!entityManager.HasComponent<CumOverlayComponent>(uid))
            return false;

        return true;
    }
}
