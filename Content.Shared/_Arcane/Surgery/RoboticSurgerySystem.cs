using Content.Shared._Arcane.Surgery.Tools;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

public sealed class RoboticSurgerySystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SurgeryToolExamineSystem _toolExamine = default!;

    private EntityQuery<RoboticBodyPartComponent> _roboticQuery;

    public override void Initialize()
    {
        base.Initialize();

        _roboticQuery = GetEntityQuery<RoboticBodyPartComponent>();

        SubscribeLocalEvent<SurgeryRoboticVariantComponent, SurgeryValidEvent>(OnVariantValid);
        SubscribeLocalEvent<SurgeryScrewdriverComponent, SurgeryToolExaminedEvent>(_toolExamine.OnExamined);
        SubscribeLocalEvent<SurgeryCrowbarComponent, SurgeryToolExaminedEvent>(_toolExamine.OnExamined);
        SubscribeLocalEvent<SurgeryWrenchComponent, SurgeryToolExaminedEvent>(_toolExamine.OnExamined);
        SubscribeLocalEvent<SurgeryMultitoolComponent, SurgeryToolExaminedEvent>(_toolExamine.OnExamined);
    }

    private void OnVariantValid(Entity<SurgeryRoboticVariantComponent> ent, ref SurgeryValidEvent args)
    {
        if (_roboticQuery.HasComp(args.Part))
            args.Cancelled = true;
    }

    /// <summary>
    ///     The surgery that has to be done first, swapped for its robotic variant on a robotic part.
    /// </summary>
    public EntProtoId? GetRequirement(SurgeryComponent surgery, EntityUid part)
    {
        if (surgery.Requirement is not { } requirement
            || !_roboticQuery.HasComp(part)
            || !_prototypes.Index(requirement).TryGetComponent<SurgeryRoboticVariantComponent>(out var variant, _factory))
            return surgery.Requirement;

        return variant.Variant;
    }
}
