// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._RMC14.LinkAccount;
using Content.Shared._Arcane.LinkAccount;
using Robust.Shared.Network;

namespace Content.Client._RMC14.LinkAccount;

public sealed class LinkAccountManager : IPostInjectInit
{
    [Dependency] private readonly INetManager _net = default!;

    private readonly List<SharedRMCPatron> _allPatrons = [];

    public SharedRMCPatronTier? Tier { get; private set; }
    public bool Linked { get; private set; }
    // arcane discord link start
    public bool HasPlayerRole { get; private set; }
    // arcane discord link end
    public Color? GhostColor { get; private set; }
    public SharedRMCGhostCosmetics? GhostCosmetics { get; private set; } // Goob - ghost cosmetics
    public SharedRMCLobbyMessage? LobbyMessage { get; private set; }
    public SharedRMCRoundEndShoutouts? RoundEndShoutout { get; private set; }

    public event Action<Guid>? CodeReceived;
    public event Action? Updated;

    private void OnCode(LinkAccountCodeMsg message)
    {
        CodeReceived?.Invoke(message.Code);
    }

    private void OnStatus(LinkAccountStatusMsg ev)
    {
        Tier = ev.Patron?.Tier;
        Linked = ev.Patron?.Linked ?? false;
        // arcane discord link start
        HasPlayerRole = ev.Patron?.HasPlayerRole ?? false;
        // arcane discord link end
        GhostColor = ev.Patron?.GhostColor;
        GhostCosmetics = ev.Patron?.GhostCosmetics; // Goob - ghost cosmetics
        LobbyMessage = ev.Patron?.LobbyMessage;
        RoundEndShoutout = ev.Patron?.RoundEndShoutout;
        Updated?.Invoke();
    }

    private void OnPatronList(RMCPatronListMsg ev)
    {
        _allPatrons.Clear();
        _allPatrons.AddRange(ev.Patrons);
    }

    public IReadOnlyList<SharedRMCPatron> GetPatrons()
    {
        return _allPatrons;
    }

    public bool CanViewPatronPerks()
    {
        return Tier is { } tier &&
               (tier.GhostColor ||
                tier.GhostCosmetics || // Goob - ghost cosmetics
                tier.GhostParticles || // Goob - ghost cosmetics
                tier.LobbyMessage ||
                tier.RoundEndShoutout);
    }

    // arcane discord link start
    public void RequestUnlink()
    {
        _net.ClientSendMessage(new LinkAccountUnlinkRequestMsg());
    }
    // arcane discord link end

    void IPostInjectInit.PostInject()
    {
        _net.RegisterNetMessage<LinkAccountCodeMsg>(OnCode);
        _net.RegisterNetMessage<LinkAccountRequestMsg>();
        _net.RegisterNetMessage<LinkAccountStatusMsg>(OnStatus);
        // arcane discord link start
        _net.RegisterNetMessage<LinkAccountUnlinkRequestMsg>();
        // arcane discord link end
        _net.RegisterNetMessage<RMCPatronListMsg>(OnPatronList);
        _net.RegisterNetMessage<RMCClearGhostColorMsg>();
        _net.RegisterNetMessage<RMCChangeGhostColorMsg>();
        _net.RegisterNetMessage<RMCChangeLobbyMessageMsg>();
        _net.RegisterNetMessage<RMCChangeNTShoutoutMsg>();
    }
}
