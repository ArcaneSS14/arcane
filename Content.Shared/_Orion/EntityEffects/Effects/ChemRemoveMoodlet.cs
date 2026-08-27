using Content.Shared._Orion.Mood;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orion.EntityEffects.Effects;

/// <summary>
/// Removes a moodlet from an entity if present.
/// </summary>
[UsedImplicitly]
public sealed partial class ChemRemoveMoodletSystem : EntityEffectSystem<MetaDataComponent, ChemRemoveMoodlet>
{
    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<ChemRemoveMoodlet> args)
    {
        RaiseLocalEvent(entity, new MoodRemoveEffectEvent(args.Effect.MoodPrototype));
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ChemRemoveMoodlet : EntityEffectBase<ChemRemoveMoodlet>
{
    /// <summary>
    /// The mood prototype to remove from the entity.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<MoodEffectPrototype> MoodPrototype;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var moodPrototype = prototype.Index<MoodEffectPrototype>(MoodPrototype.Id);
        return Loc.GetString("reagent-effect-guidebook-remove-moodlet",
            ("name", moodPrototype.Description()));
    }
}
