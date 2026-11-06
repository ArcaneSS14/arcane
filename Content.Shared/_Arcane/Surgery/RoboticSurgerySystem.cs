using Content.Shared._Arcane.Surgery.Tools;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.Surgery;

public sealed class RoboticSurgerySystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
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
        SubscribeLocalEvent<SurgeryWirecutterComponent, SurgeryToolExaminedEvent>(_toolExamine.OnExamined);
        SubscribeLocalEvent<SurgeryCableComponent, SurgeryToolExaminedEvent>(_toolExamine.OnExamined);
        SubscribeLocalEvent<SurgeryWelderComponent, SurgeryToolExaminedEvent>(_toolExamine.OnExamined);
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

        return variant.Variant ?? surgery.Requirement;
    }

    /// <summary>
    ///     The tool a step needs on this part, with surgical tools swapped for mechanical ones on a robotic part.
    /// </summary>
    public ComponentRegistry? GetTool(Entity<SurgeryStepComponent?> step, EntityUid part)
    {
        if (!Resolve(step, ref step.Comp, false) || step.Comp.Tool is not { } tool)
            return null;

        return GetRoboticTool(tool, part)?.Tool ?? tool;
    }

    /// <summary>
    ///     The icon of the mechanical tool a step needs on a robotic part, null when the step keeps its own.
    /// </summary>
    public SpriteSpecifier? GetIcon(Entity<SurgeryStepComponent?> step, EntityUid part)
    {
        if (!Resolve(step, ref step.Comp, false) || step.Comp.Tool is not { } tool)
            return null;

        return GetRoboticTool(tool, part)?.Icon;
    }

    /// <summary>
    ///     The step's name, worded for machinery on a robotic part when the step has such a name.
    /// </summary>
    public string GetStepName(EntityUid step, EntityUid part)
    {
        if (_roboticQuery.HasComp(part)
            && Prototype(step) is { } proto
            && Loc.TryGetString($"surgery-robotic-step-{proto.ID}", out var name))
            return name;

        return Name(step);
    }

    /// <summary>
    ///     The locale key worded for machinery on a robotic part, when such a wording exists.
    /// </summary>
    public string GetRoboticKey(string key, EntityUid part)
    {
        if (_roboticQuery.HasComp(part) && _loc.HasString($"{key}-robotic"))
            return $"{key}-robotic";

        return key;
    }

    private SurgeryRoboticToolPrototype? GetRoboticTool(ComponentRegistry tool, EntityUid part)
    {
        if (!_roboticQuery.HasComp(part))
            return null;

        foreach (var name in tool.Keys)
        {
            if (_prototypes.TryIndex<SurgeryRoboticToolPrototype>(name, out var robotic))
                return robotic;
        }

        return null;
    }
}
