using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

/// <summary>
/// Requires a tail, i.e. any tail marking. Works for every species, including humans.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class TailRequirement : InvertableErpRequirement
{
    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        var hasTail = entityManager.TryGetComponent<HumanoidAppearanceComponent>(uid, out var humanoid)
            && humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Tail, out var markings)
            && markings.Count > 0;

        return Inverted ? !hasTail : hasTail;
    }
}