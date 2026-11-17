using Content.Shared.Disposal.Components;
using Robust.Shared.Containers;

namespace Content.Shared._Arcane.Disposal.Unit;
public sealed partial class DisposalUnitAnchorGuardSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnInsertingAttempt(Entity<DisposalUnitComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.Container is { } container && args.Container != container)
            return;

        if (!Transform(args.EntityUid).Anchored)
            return;

        args.Cancel();
    }
}
