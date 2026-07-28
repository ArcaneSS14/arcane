// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Orion.Research;
using Content.Shared._Orion.Research.Prototypes;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Research.Prototypes;

/// <summary>
/// This is a prototype for a technology that can be unlocked.
/// </summary>
[Prototype]
public sealed partial class TechnologyPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The name of the technology.
    /// Supports locale strings
    /// </summary>
    [DataField(required: true)]
    public LocId Name = string.Empty;

    // Orion-Start
    /// <summary>
    /// Localized description of the technology.
    /// </summary>
    [DataField]
    public LocId Description = string.Empty;
    // Orion-End

    /// <summary>
    /// An icon used to visually represent the technology in UI.
    /// </summary>
    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;

    /// <summary>
    /// What research discipline this technology belongs to.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<TechDisciplinePrototype> Discipline;

    /// <summary>
    /// What tier research is this?
    /// The tier governs how much lower-tier technology
    /// needs to be unlocked before this one.
    /// </summary>
    [DataField(required: true)]
    public int Tier;

    /// <summary>
    /// Hidden tech is not ever available at the research console.
    /// </summary>
    [DataField]
    public bool Hidden;

    // Orion-Start
    /// <summary>
    /// Should this technology start as researched in every compatible database.
    /// </summary>
    [DataField]
    public bool StartingTechnology;
    // Orion-End

/* // Orion-Edit: Use PointCosts
    /// <summary>
    /// How much research is needed to unlock.
    /// </summary>
    [DataField]
    public int Cost = 10000;
*/

    // Orion-Start
    /// <summary>
    /// Multipoint type costs.
    /// </summary>
    [DataField(required: true)]
    public List<ResearchPointAmount> PointCosts = new();
    // Orion-End

    /// <summary>
    /// A list of <see cref="TechnologyPrototype"/>s that need to be unlocked in order to unlock this technology.
    /// </summary>
    [DataField]
    public List<ProtoId<TechnologyPrototype>> TechnologyPrerequisites = new();

    // Orion-Start
    /// <summary>
    /// The required experiments that must be completed before this technology can be researched.
    /// </summary>
    [DataField]
    public List<string> RequiredExperiments = new();

    /// <summary>
    /// Experiments that can reduce the research cost for this technology.
    /// </summary>
    [DataField]
    public List<string> DiscountExperiments = new();

    /// <summary>
    /// Per-technology discount values keyed by experiment ID.
    /// </summary>
    [DataField]
    public Dictionary<string, int> DiscountExperimentCosts = new();

    /// <summary>
    /// Experiments unlocked when this technology is researched.
    /// </summary>
    [DataField]
    public List<string> UnlockedExperiments = new();

    /// <summary>
    /// Should this technology be announced to the station when unlocked.
    /// </summary>
    [DataField]
    public bool AnnounceOnUnlock = true;

    /// <summary>
    /// Radio channels used for unlock announcements when <see cref="AnnounceOnUnlock"/> is enabled.
    /// Falls back to the console announcement channel if empty.
    /// </summary>
    [DataField]
    public List<ProtoId<RadioChannelPrototype>> AnnounceChannels = new();

    /// <summary>
    /// Indicates a technology that primarily unlocks RnD infrastructure.
    /// </summary>
    [DataField]
    public bool InfrastructureUnlock;

    /// <summary>
    /// Infrastructure categories unlocked by this technology.
    /// </summary>
    [DataField]
    public List<string> InfrastructureUnlocks = new();

    /// <summary>
    /// Additional reveal/discovery requirements for hidden technologies.
    /// </summary>
    [DataField]
    public List<TechnologyRevealRequirement> RevealRequirements = new();
    // Orion-End

    /// <summary>
    /// A list of <see cref="LatheRecipePrototype"/>s that are unlocked by this technology
    /// </summary>
    [DataField]
    public List<ProtoId<LatheRecipePrototype>> RecipeUnlocks = new();

    /// <summary>
    /// A list of non-standard effects that are done when this technology is unlocked.
    /// </summary>
    [DataField]
    public IReadOnlyList<GenericUnlock> GenericUnlocks = new List<GenericUnlock>();

    // Orion-Start
    /// <summary>
    /// Future-proof unlock list for item-discovery style systems.
    /// </summary>
    [DataField]
    public List<string> ItemUnlocks = new();

    /// <summary>
    /// Future-proof unlock list for deconstruction discovery systems.
    /// </summary>
    [DataField]
    public List<string> DeconstructionUnlocks = new();

    /// <summary>
    /// Required item prototype paths to reveal or unlock this technology.
    /// </summary>
    [DataField]
    public List<string> RequiredItemsToUnlock = new();
    // Orion-End

    /// <summary>
    /// Goobstation RnD console rework field
    /// Position of this tech in console menu
    /// </summary>
    [DataField(required: true)]
    public Vector2i Position { get; private set; }

    // Orion-Start
    public IEnumerable<ProtoId<TechnologyPrototype>> AllRequiredTechnologies => TechnologyPrerequisites;
    // Orion-End
}

// Orion-Start
[Serializable, NetSerializable]
public enum TechnologyRevealRequirementKind : byte
{
    RevealedTechnology,
    ResearchedTechnology,
    CompletedExperiment,
    ScanEntity,
    MachineInsertion,
    DeconstructEntity,
    ServerTrigger,
}

[DataDefinition, Serializable, NetSerializable]
[ImplicitDataDefinitionForInheritors]
public abstract partial record TechnologyRevealRequirement
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField]
    public TechnologyRevealRequirementKind Kind;

    [DataField]
    public int Target = 1;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial record RevealedTechnologyRevealRequirement : TechnologyRevealRequirement
{
    [DataField(required: true)]
    public ProtoId<TechnologyPrototype> Technology;

    public RevealedTechnologyRevealRequirement()
    {
        Kind = TechnologyRevealRequirementKind.RevealedTechnology;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial record ResearchedTechnologyRevealRequirement : TechnologyRevealRequirement
{
    [DataField(required: true)]
    public ProtoId<TechnologyPrototype> Technology;

    public ResearchedTechnologyRevealRequirement()
    {
        Kind = TechnologyRevealRequirementKind.ResearchedTechnology;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial record CompletedExperimentRevealRequirement : TechnologyRevealRequirement
{
    [DataField(required: true)]
    public ProtoId<ResearchExperimentPrototype> Experiment;

    public CompletedExperimentRevealRequirement()
    {
        Kind = TechnologyRevealRequirementKind.CompletedExperiment;
    }
}

[DataDefinition, Serializable, NetSerializable]
public partial record ScanEntityRevealRequirement : TechnologyRevealRequirement
{
    [DataField]
    public string? RequiredEntityPrototype;

    [DataField]
    public List<string> RequiredTags = new();

    public ScanEntityRevealRequirement()
    {
        Kind = TechnologyRevealRequirementKind.ScanEntity;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial record MachineInsertionRevealRequirement : ScanEntityRevealRequirement
{
    [DataField]
    public string? RequiredMachinePrototype;

    public MachineInsertionRevealRequirement()
    {
        Kind = TechnologyRevealRequirementKind.MachineInsertion;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial record DeconstructEntityRevealRequirement : TechnologyRevealRequirement
{
    [DataField]
    public string? RequiredEntityPrototype;

    [DataField]
    public List<string> RequiredTags = new();

    public DeconstructEntityRevealRequirement()
    {
        Kind = TechnologyRevealRequirementKind.DeconstructEntity;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial record ServerTriggerRevealRequirement : TechnologyRevealRequirement
{
    [DataField(required: true)]
    public string TriggerId = string.Empty;

    public ServerTriggerRevealRequirement()
    {
        Kind = TechnologyRevealRequirementKind.ServerTrigger;
    }
}
// Orion-End

[DataDefinition]
public partial record struct GenericUnlock()
{
    /// <summary>
    /// What event is raised when this is unlocked?
    /// Used for doing non-standard logic.
    /// </summary>
    [DataField]
    public object? PurchaseEvent = null;

    /// <summary>
    /// A player facing tooltip for what the unlock does.
    /// Supports locale strings.
    /// </summary>
    [DataField]
    public string UnlockDescription = string.Empty;
}
