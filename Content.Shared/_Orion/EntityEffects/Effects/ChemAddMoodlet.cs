using Content.Shared._Orion.Mood;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orion.EntityEffects.Effects;

/// <summary>
/// Adds a moodlet to an entity.
/// </summary>
[UsedImplicitly]
public sealed partial class ChemAddMoodletSystem : EntityEffectSystem<MetaDataComponent, ChemAddMoodlet>
{
    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<ChemAddMoodlet> args)
    {
        RaiseLocalEvent(entity, new MoodEffectEvent(args.Effect.MoodPrototype));
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ChemAddMoodlet : EntityEffectBase<ChemAddMoodlet>
{
    /// <summary>
    /// The mood prototype to apply to the entity.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<MoodEffectPrototype> MoodPrototype;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var moodPrototype = prototype.Index<MoodEffectPrototype>(MoodPrototype.Id);
        return Loc.GetString("reagent-effect-guidebook-add-moodlet",
            ("amount", moodPrototype.MoodChange),
            ("timeout", moodPrototype.Timeout));
    }
}
