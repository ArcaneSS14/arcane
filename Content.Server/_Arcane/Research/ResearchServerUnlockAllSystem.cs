using Content.Server.Research.Systems;
using Content.Shared.Research.Components;

namespace Content.Server._Arcane.Research;

public sealed class ResearchServerUnlockAllSystem : EntitySystem
{
    [Dependency] private readonly ResearchSystem _research = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ResearchServerUnlockAllComponent, MapInitEvent>(OnMapInit, after: [typeof(ResearchSystem)]);
    }

    private void OnMapInit(Entity<ResearchServerUnlockAllComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<ResearchServerComponent>(ent, out var server))
            return;

        _research.UnlockAllTechnologiesOnServer(ent, server);
    }
}
