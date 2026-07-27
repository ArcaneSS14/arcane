using Content.Shared._Orion.Mood;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orion.EntityEffects.Effects;

/// <summary>
/// Removes non-categorized moodlets from an entity.
/// </summary>
[UsedImplicitly]
public sealed partial class ChemPurgeMoodletsSystem : EntityEffectSystem<MetaDataComponent, ChemPurgeMoodlets>
{
    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<ChemPurgeMoodlets> args)
    {
        RaiseLocalEvent(entity, new MoodPurgeEffectsEvent(args.Effect.RemovePermanentMoodlets));
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ChemPurgeMoodlets : EntityEffectBase<ChemPurgeMoodlets>
{
    [DataField]
    public bool RemovePermanentMoodlets;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-purge-moodlets");
}
