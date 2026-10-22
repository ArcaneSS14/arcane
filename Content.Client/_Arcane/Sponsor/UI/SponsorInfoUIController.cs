using System.Numerics;
using Content.Client.Lobby;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Network;

namespace Content.Client._Arcane.Sponsor.UI;

public sealed class SponsorInfoUIController : UIController, IOnStateEntered<LobbyState>, IOnStateExited<LobbyState>
{
    [Dependency] private readonly INetManager _net = default!;

    private SponsorInfoWindow? _window;
    private bool _shownThisConnection;

    public override void Initialize()
    {
        base.Initialize();
        _net.Disconnect += OnDisconnected;
    }

    public void OnStateEntered(LobbyState state)
    {
        if (_shownThisConnection)
            return;

        _shownThisConnection = true;
        OpenWindow();
    }

    public void OnStateExited(LobbyState state)
    {
        _window?.Close();
    }

    public void ToggleWindow()
    {
        if (_window == null)
            OpenWindow();
        else
            _window.Close();
    }

    private void OpenWindow()
    {
        if (_window != null)
            return;

        _window = new SponsorInfoWindow();
        _window.OnClose += () => _window = null;
        _window.OpenCenteredAt(new Vector2(0.5f, 0.28f));
    }

    private void OnDisconnected(object? sender, NetDisconnectedArgs args)
    {
        _shownThisConnection = false;
        _window?.Close();
    }
}
