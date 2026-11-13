using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ErpPanel.Requirements;


public sealed partial class HasAnyTagRequirement : ErpRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<TagPrototype>> Tags = new();

    public override bool IsAvailable(EntityUid uid, IEntityManager entMan)
    {
        var tagSystem = entMan.System<TagSystem>();
        return tagSystem.HasAnyTag(uid, Tags);
    }
}
