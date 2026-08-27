using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class TargetOnFloorRequirement : ErpRequirement
{
    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        if (!entityManager.System<StandingStateSystem>().IsDown(uid))
            return false;

        return true;
    }
}
