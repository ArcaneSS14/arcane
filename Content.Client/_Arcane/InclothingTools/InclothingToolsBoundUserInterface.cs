using Content.Shared._Arcane.InclothingTools;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;

namespace Content.Client._Arcane.InclothingTools;

public sealed class InclothingToolsBoundUserInterface : BoundUserInterface
{

    [Dependency] private readonly IClyde _displayManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;

    private IEntityManager _entityManager;

    private InclothingToolsRadialMenu? _menu;

    public InclothingToolsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        _entityManager = IoCManager.Resolve<EntityManager>();
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<InclothingToolsRadialMenu>();
        _menu.SetEntity(Owner);
        _menu.SendSelectToolMessageAction += SendSelectToolsMessage;

        var vpSize = _displayManager.ScreenSize;
        _menu.OpenCenteredAt(_inputManager.MouseScreenPosition.Position / vpSize);

        _menu.RefreshUi();
    }

    private void SendSelectToolsMessage(EntityUid uid)
    {
        var message = new InclothingToolsUiMessage(_entityManager.GetNetEntity(uid));
        SendPredictedMessage(message);
    }
}