using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Cloning.Events;
using Content.Shared.PDA;

namespace Content.Server._Arcane.Roles;

/// <summary>
///     Copies the alternate job title from a character's ID card onto the clone's ID card.
///     Cloning spawns card/PDA from prototypes, so the per-entity title would otherwise be lost.
/// </summary>
public sealed class AltTitleCloneSystem : EntitySystem
{
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PdaComponent, CloningItemStoragePopulatedEvent>(OnClonePda);
    }

    private void OnClonePda(Entity<PdaComponent> original, ref CloningItemStoragePopulatedEvent args)
    {
        if (original.Comp.ContainedId is not { } originalId
            || !TryComp<IdCardComponent>(originalId, out var card)
            || string.IsNullOrEmpty(card.LocalizedJobTitle)
            || TerminatingOrDeleted(args.CloneUid)
            || !TryComp<PdaComponent>(args.CloneUid, out var clonePda)
            || clonePda.ContainedId is not { } cloneId)
            return;

        _idCard.TryChangeJobTitle(cloneId, card.LocalizedJobTitle);
    }
}