using Content.Shared._Arcane.PlantAnalyzer;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client._Arcane.PlantAnalyzer;

public sealed class PlantAnalyzerBoundUserInterface : BoundUserInterface
{
    private PlantAnalyzerWindow? _window;

    public PlantAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new PlantAnalyzerWindow();
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not PlantAnalyzerUserInterfaceState castState)
            return;

        _window?.Populate(castState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        if (_window != null)
        {
            _window.OnClose -= Close;
            _window.Dispose();
        }
    }
}
