using Content.Shared._Arcane.ERP.Preferences;
using Robust.Shared.Network;

namespace Content.Client._Arcane.ERP.Preferences;

public sealed class ClientErpOrganPreferencesManager : IPostInjectInit
{
    [Dependency] private readonly INetManager _net = default!;

    private readonly Dictionary<int, ErpOrganPreferences> _slots = new();

    public event Action<int, ErpOrganPreferences>? OnPreferencesReceived;

    public void PostInject()
    {
        _net.RegisterNetMessage<MsgErpOrganPreferences>(HandleReceived);
        _net.RegisterNetMessage<MsgUpdateErpOrganPreferences>();
        _net.Disconnect += OnDisconnect;
    }

    private void OnDisconnect(object? sender, NetDisconnectedArgs e)
    {
        _slots.Clear();
    }

    public ErpOrganPreferences GetSlot(int slot)
        => (_slots.TryGetValue(slot, out var p) ? p : ErpOrganPreferences.Default()).Clone();

    public void SaveSlot(int slot, ErpOrganPreferences prefs)
    {
        _slots[slot] = prefs;
        var msg = new MsgUpdateErpOrganPreferences { Slot = slot, Preferences = prefs };
        _net.ClientSendMessage(msg);
    }

    private void HandleReceived(MsgErpOrganPreferences msg)
    {
        _slots[msg.Slot] = msg.Preferences;
        OnPreferencesReceived?.Invoke(msg.Slot, msg.Preferences);
    }
}
