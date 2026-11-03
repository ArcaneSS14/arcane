using Content.Shared._Arcane.Polymorph;
using Content.Shared.Interaction;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.Polymorph;

public sealed partial class PettingAnimationSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IGameTiming _timing = default!;

    [SubscribeLocalEvent]
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
