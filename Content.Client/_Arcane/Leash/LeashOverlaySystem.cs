using Robust.Client.Graphics;

namespace Content.Client._Arcane.Leash;

public sealed class LeashSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private LeashOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new LeashOverlay(EntityManager);
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
    }
}
