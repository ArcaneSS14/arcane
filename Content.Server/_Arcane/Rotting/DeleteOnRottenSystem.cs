using Content.Shared.Atmos.Rotting;
using Content.Shared.Humanoid;

namespace Content.Server._Arcane.Rotting;

public sealed partial class DeleteOnRottenSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RottingComponent, IsRottingEvent>(OnRotting);
    }

    private void OnRotting(EntityUid uid, RottingComponent component, IsRottingEvent args)
    {
        if (HasComp<HumanoidAppearanceComponent>(uid))
            return;

        if (component.TotalRotTime > component.DeleteAfter)
            QueueDel(uid);
    }
}
