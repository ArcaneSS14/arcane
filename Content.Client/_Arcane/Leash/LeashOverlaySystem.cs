using Content.Shared._Arcane.Leash;
using Robust.Client.Graphics;

namespace Content.Client._Arcane.Leash;

public sealed class LeashSystem : SharedLeashSystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private LeashOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new LeashOverlay(EntityManager);
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
        {
            _overlayManager.RemoveOverlay(_overlay);
            _overlay = null;
        }
    }
}
