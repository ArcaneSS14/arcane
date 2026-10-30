using Content.Shared._Arcane.Polymorph;
using Content.Shared.Interaction;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.Polymorph;

public sealed class PettingAnimationSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PettingAnimationComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInteractHand(EntityUid uid, PettingAnimationComponent component, InteractHandEvent args)
    {
        _appearance.SetData(uid, AnimalVisuals.Petting, true);

        Timer.Spawn(TimeSpan.FromSeconds(component.Duration), () =>
        {
            if (!Exists(uid))
                return;

            _appearance.SetData(uid, AnimalVisuals.Petting, false);
        });
    }
}
