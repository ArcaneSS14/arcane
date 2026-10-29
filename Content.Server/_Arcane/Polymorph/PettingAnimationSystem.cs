using Content.Shared._Arcane.Polymorph;
using Content.Shared.Interaction.Events;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.Polymorph;

public sealed class PettingAnimationSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PettingAnimationComponent, InteractionSuccessEvent>(OnInteractionSuccess);
    }
    private void OnInteractionSuccess(EntityUid uid, PettingAnimationComponent component, ref InteractionSuccessEvent args)
    {
        _appearance.SetData(uid, AnimalVisuals.Petting, true);

        Timer.Spawn(TimeSpan.FromSeconds(component.Duration), () =>
        {
            if (Exists(uid))
                _appearance.SetData(uid, AnimalVisuals.Petting, false);
        });
    }
}
