using Content.Shared._Arcane.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

/// <summary>
/// Gates surgeries on the research of the surgeon's own network, wherever the surgeon and the patient are.
/// </summary>
public abstract class SharedSurgeryResearchSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryTechnologyConditionComponent, SurgeryValidEvent>(OnTechnologyValid);
    }

    // The shared choices list has no surgeon, so research is checked when a surgeon looks at it or operates.
    private void OnTechnologyValid(Entity<SurgeryTechnologyConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (args.Cancelled || args.User is not { } user)
            return;

        if (!IsResearchedFor((ent, ent.Comp), user))
            args.Cancelled = true;
    }

    /// <summary>
    /// Whether the surgeon's research network allows the surgery; surgeries without a technology condition always do.
    /// </summary>
    public bool IsResearchedFor(Entity<SurgeryTechnologyConditionComponent?> surgery, EntityUid user)
    {
        if (!Resolve(surgery, ref surgery.Comp, false))
            return true;

        var researched = IsTechnologyUnlocked(user, surgery.Comp.Technology);
        var replaced = surgery.Comp.ReplacedBy is { } replacement && IsTechnologyUnlocked(user, replacement);
        return !replaced && researched != surgery.Comp.Inverse;
    }

    protected abstract bool IsTechnologyUnlocked(EntityUid user, ProtoId<TechnologyPrototype> technology);
}
