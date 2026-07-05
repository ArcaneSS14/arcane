// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <93730715+Aviu00@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 DrSmugleaf <10968691+DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 DrSmugleaf <drsmugleaf@gmail.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Ichaie <167008606+Ichaie@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Ilya246 <57039557+Ilya246@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 JORJ949 <159719201+JORJ949@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 MortalBaguette <169563638+MortalBaguette@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Panela <107573283+AgentePanela@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 Poips <Hanakohashbrown@gmail.com>
// SPDX-FileCopyrightText: 2025 PuroSlavKing <103608145+PuroSlavKing@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 Whisper <121047731+QuietlyWhisper@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 blobadoodle <me@bloba.dev>
// SPDX-FileCopyrightText: 2025 coderabbitai[bot] <136622811+coderabbitai[bot]@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2025 github-actions[bot] <41898282+github-actions[bot]@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 kamkoi <poiiiple1@gmail.com>
// SPDX-FileCopyrightText: 2025 shibe <95730644+shibechef@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 tetra <169831122+Foralemes@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using System.Threading.Tasks;
using Content.Server.Connection;
using Content.Goobstation.Common.CCVar;
using Content.Server.Database;
using Content.Shared.CCVar;
using Content.Shared._Arcane.Sponsor;
using Content.Shared._Arcane.LinkAccount;
using Content.Shared._RMC14.LinkAccount;
using Robust.Server.Player;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Color = System.Drawing.Color;

namespace Content.Server._RMC14.LinkAccount;

public sealed class LinkAccountManager : IPostInjectInit, ISharedSponsorManager
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly INetManager _net = default!;
    // arcane discord link start
    [Dependency] private readonly IServerNetManager _serverNet = default!;
    // arcane discord link end
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UserDbDataManager _userDb = default!;
    // arcane discord link start
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly ITaskManager _task = default!;
    // arcane discord link end

    private readonly Dictionary<NetUserId, TimeSpan> _lastRequest = new();
    private readonly TimeSpan _minimumWait = TimeSpan.FromSeconds(0.5);
    private readonly Dictionary<NetUserId, SharedRMCPatronFull> _connected = new();
    private readonly Dictionary<string, SharedRMCPatronTier> _fauxTiers = new();
    private readonly Dictionary<NetUserId, string> _fauxPatronAssignments = new();
    private readonly List<SharedRMCPatron> _allPatrons = [];
    private readonly List<(string Message, string User)> _lobbyMessages = [];
    private readonly List<string> _shoutouts = [];

    // arcane discord link start
    private const string DiscordLinkRequiredKey = "discord_link_required";
    private const string DiscordPlayerRoleRequiredKey = "discord_player_role_required";
    private const string DiscordLinkCodeKey = "discord_link_code";
    private const string DiscordInviteLinkKey = "discord_invite_link";
    // arcane discord link end

    public event Action? PatronsReloaded;
    public event Action<(NetUserId Id, SharedRMCPatronFull Patron)>? PatronUpdated;

    private async Task LoadData(ICommonSession player, CancellationToken cancel)
    {
        var patron = await _db.GetPatron(player.UserId, cancel);
        // arcane discord link start
        var linked = await _db.GetLinkedAccountStatus(player.UserId, cancel);
        // arcane discord link end
        cancel.ThrowIfCancellationRequested();

        var tier = patron?.Tier;
        var sharedTier = tier == null
            ? null
            : new SharedRMCPatronTier(
                tier.ShowOnCredits,
                tier.GhostColor,
                tier.LobbyMessage,
                tier.RoundEndShoutout,
                tier.Name,
                tier.Icon
            );

        SharedRMCLobbyMessage? lobbyMessage = null;
        if (patron?.LobbyMessage is { Message.Length: > 0 } patronMsg)
            lobbyMessage = new SharedRMCLobbyMessage(patronMsg.Message);

        var ntName = patron?.RoundEndNTShoutout?.Name;
        SharedRMCRoundEndShoutouts? shoutouts = null;
        if (ntName != null)
            shoutouts = new SharedRMCRoundEndShoutouts(ntName);


        Robust.Shared.Maths.Color? ghostColor = null;
        if (patron?.GhostColor is { } patronColor)
        {
            var sysColor = Color.FromArgb(patronColor);
            ghostColor = new Robust.Shared.Maths.Color(sysColor.R, sysColor.G, sysColor.B, sysColor.A);
        }

        // arcane discord link start
        _connected[player.UserId] = new SharedRMCPatronFull(
            sharedTier,
            linked.Linked,
            linked.HasPlayerRole,
            ghostColor,
            lobbyMessage,
            shoutouts);
        // arcane discord link end
    }

    private void FinishLoad(ICommonSession player)
    {
        SendPatronStatus(player);
    }

    private void ClientDisconnected(ICommonSession player)
    {
        _connected.Remove(player.UserId);
    }

    private void SendPatronStatus(ICommonSession player)
    {
        var connected = _connected.GetValueOrDefault(player.UserId);
        var msg = new LinkAccountStatusMsg { Patron = connected, };
        _net.ServerSendMessage(msg, player.Channel);
        SendPatrons(player);
    }

    // arcane discord link start
    private async void OnRequest(LinkAccountRequestMsg message)
    // arcane discord link end
    {
        var user = message.MsgChannel.UserId;
        var time = _timing.RealTime;
        if (_lastRequest.TryGetValue(user, out var last) &&
            last + _minimumWait > time)
        {
            return;
        }

        _lastRequest[user] = time;

        var code = Guid.NewGuid();
        // arcane discord link start
        await _db.SetLinkingCode(user, code);
        // arcane discord link end

        var response = new LinkAccountCodeMsg { Code = code };
        _net.ServerSendMessage(response, message.MsgChannel);
    }

    // arcane discord link start
    private async void OnUnlinkRequest(LinkAccountUnlinkRequestMsg message)
    {
        if (!_player.TryGetSessionById(message.MsgChannel.UserId, out var session))
            return;

        await _db.UnlinkDiscordAccount(session.UserId, CancellationToken.None);
        await LoadData(session, CancellationToken.None);
        SendPatronStatus(session);
        session.Channel.Disconnect(Loc.GetString("rmc-ui-discord-account-unlinked-kick"));
    }

    private async Task OnConnecting(NetConnectingArgs args)
    {
        if (args.IsDenied)
            return;

        if (!_config.GetCVar(GoobCVars.RMCDiscordAccountLinkRequired))
            return;

        var status = await _db.GetLinkedAccountStatus(args.UserId, CancellationToken.None);
        if (!status.Linked)
        {
            await DenyConnection(args, "rmc-ui-discord-link-required", DiscordLinkRequiredKey);
            return;
        }

        if (_config.GetCVar(GoobCVars.RMCDiscordAccountPlayerRoleRequired) &&
            !status.HasPlayerRole)
        {
            await DenyConnection(args, "rmc-ui-discord-player-role-required", DiscordPlayerRoleRequiredKey);
        }
    }

    private async Task DenyConnection(NetConnectingArgs args, string locId, string denyKey)
    {
        var code = Guid.NewGuid();
        await _db.UpdatePlayerRecordAsync(args.UserId, args.UserName, args.IP.Address, args.UserData.GetModernHwid());
        await _db.SetLinkingCode(args.UserId, code);

        var codeText = code.ToString();
        var inviteLink = _config.GetCVar(GoobCVars.RMCDiscordAccountLinkingMessageLink);
        if (string.IsNullOrWhiteSpace(inviteLink))
            inviteLink = _config.GetCVar(CCVars.InfoLinksDiscord);

        var properties = new Dictionary<string, object>
        {
            [denyKey] = true,
            [DiscordLinkCodeKey] = codeText,
        };

        if (!string.IsNullOrEmpty(inviteLink))
            properties[DiscordInviteLinkKey] = inviteLink;

        var reason = Loc.GetString(
            $"{locId}-with-code",
            ("code", codeText),
            ("command", $"/link code:{codeText}"));

        if (!string.IsNullOrEmpty(inviteLink))
            reason += "\n" + Loc.GetString("rmc-ui-discord-invite-link", ("invite", inviteLink));

        args.Deny(new NetDenyReason(reason, properties));
    }
    // arcane discord link end

    // arcane sponsor start
    private void OnSponsorUpdated(DatabaseNotification notification)
    {
        if (notification.Channel != ArcaneSponsorTiers.UpdatedNotificationChannel ||
            notification.Payload == null ||
            !Guid.TryParse(notification.Payload, out var playerId))
        {
            return;
        }

        _task.RunOnMainThread(() => ReloadSponsor(playerId));
    }

    private async void ReloadSponsor(Guid playerId)
    {
        if (_player.TryGetSessionById(new NetUserId(playerId), out var session))
        {
            await LoadData(session, CancellationToken.None);
            SendPatronStatus(session);

            if (_connected.TryGetValue(session.UserId, out var patron))
                PatronUpdated?.Invoke((session.UserId, patron));
        }

        await RefreshAllPatrons();
        SendPatronsToAll();
    }
    // arcane sponsor end

    private void OnClearGhostColor(RMCClearGhostColorMsg message)
    {
        SetGhostColor(message.MsgChannel.UserId, null);
    }

    private void OnChangeGhostColor(RMCChangeGhostColorMsg message)
    {
        SetGhostColor(message.MsgChannel.UserId, message.Color);
    }

    private void OnChangeLobbyMessage(RMCChangeLobbyMessageMsg message)
    {
        var text = message.Text;
        if (text == null)
            return;

        var user = message.MsgChannel.UserId;
        if (GetPatron(user)?.Tier is not { LobbyMessage: true })
            return;

        if (text.Length > SharedRMCLobbyMessage.CharacterLimit)
            text = text[..SharedRMCLobbyMessage.CharacterLimit];

        _db.SetLobbyMessage(user, text);
    }

    private void OnChangeNTShoutout(RMCChangeNTShoutoutMsg message)
    {
        var name = message.Name;
        if (name == null)
            return;

        var user = message.MsgChannel.UserId;
        if (GetPatron(user)?.Tier is not { RoundEndShoutout: true })
            return;

        if (name.Length > SharedRMCRoundEndShoutouts.CharacterLimit)
            name = name[..SharedRMCRoundEndShoutouts.CharacterLimit];

        _db.SetNTShoutout(user, name);
    }

    private void SetGhostColor(NetUserId user, Robust.Shared.Maths.Color? color)
    {
        if (GetPatron(user)?.Tier is not { GhostColor: true })
            return;

        Color? sysColor = color == null ? null : Color.FromArgb(color.Value.ToArgb());
        _db.SetGhostColor(user, sysColor);

        if (_connected.TryGetValue(user, out var connected))
        {
            connected = connected with { GhostColor = color };
            _connected[user] = connected;
            PatronUpdated?.Invoke((user, connected));
        }
    }

    public async Task RefreshAllPatrons()
    {
        var patrons = await _db.GetAllPatrons();
        var messages = await _db.GetLobbyMessages();
        var shoutouts = await _db.GetShoutouts();

        _allPatrons.Clear();
        _lobbyMessages.Clear();
        _shoutouts.Clear();

        foreach (var patron in patrons)
        {
            _allPatrons.Add(new SharedRMCPatron(patron.Player.LastSeenUserName, patron.Tier.Name));
        }

        _lobbyMessages.AddRange(messages);
        _shoutouts.AddRange(shoutouts);

        PatronsReloaded?.Invoke();
    }

    public (string Message, string User)? GetRandomLobbyMessage()
    {
        if (_lobbyMessages.Count == 0)
            return null;

        return _random.Pick(_lobbyMessages);
    }

    public string GetRandomShoutout()
    {
        if (_shoutouts.Count == 0)
            return "John Nanotrasen";

        return _random.Pick(_shoutouts);
    }

    public void SendPatronsToAll()
    {
        var msg = new RMCPatronListMsg { Patrons = _allPatrons };
        _net.ServerSendToAll(msg);
    }

    public void SendPatrons(ICommonSession player)
    {
        var msg = new RMCPatronListMsg { Patrons = _allPatrons };
        _net.ServerSendMessage(msg, player.Channel);
    }

    public SharedRMCPatronFull? GetPatron(ICommonSession player)
    {
        return GetPatron(player.UserId);
    }

    public bool HasSponsor(ICommonSession player, string? tier = null)
    {
        return ArcaneSponsorTiers.HasTier(GetPatron(player)?.Tier?.Tier, tier);
    }

    public SharedRMCPatronFull? GetPatron(NetUserId userId)
    {
        if (_fauxPatronAssignments.TryGetValue(userId, out var tierId) &&
            _fauxTiers.TryGetValue(tierId, out var tier))
        {
            return new SharedRMCPatronFull(
                Tier: tier,
                Linked: true,
                // arcane discord link start
                HasPlayerRole: true,
                // arcane discord link end
                GhostColor: null,
                LobbyMessage: null,
                RoundEndShoutout: null
            );
        }

        return _connected.GetValueOrDefault(userId);
    }

    // arcane discord link start
    public bool CanPlay(ICommonSession player, out string locId)
    {
        locId = string.Empty;

        if (!_config.GetCVar(GoobCVars.RMCDiscordAccountLinkRequired))
            return true;

        var status = GetPatron(player.UserId);
        if (status is not { Linked: true })
        {
            locId = "rmc-ui-discord-link-required";
            return false;
        }

        if (_config.GetCVar(GoobCVars.RMCDiscordAccountPlayerRoleRequired) &&
            !status.HasPlayerRole)
        {
            locId = "rmc-ui-discord-player-role-required";
            return false;
        }

        return true;
    }
    // arcane discord link end

    public void AddFauxTier(string tierId, SharedRMCPatronTier tier)
    {
        _fauxTiers[tierId] = tier;
    }

    public bool RemoveFauxTier(string tierId)
    {
        return _fauxTiers.Remove(tierId);
    }

    public void AssignFauxPatron(NetUserId userId, string? tierId)
    {
        if (tierId == null)
            _fauxPatronAssignments.Remove(userId);
        else if (_fauxTiers.ContainsKey(tierId))
            _fauxPatronAssignments[userId] = tierId;
    }

    public Dictionary<string, SharedRMCPatronTier> GetAllFauxTiers()
    {
        return _fauxTiers;
    }

    public Dictionary<NetUserId, string> GetAllFauxPatronAssignments()
    {
        return _fauxPatronAssignments;
    }

    void IPostInjectInit.PostInject()
    {
        _net.RegisterNetMessage<LinkAccountRequestMsg>(OnRequest);
        _net.RegisterNetMessage<LinkAccountCodeMsg>();
        _net.RegisterNetMessage<LinkAccountStatusMsg>();
        // arcane discord link start
        _net.RegisterNetMessage<LinkAccountUnlinkRequestMsg>(OnUnlinkRequest);
        // arcane discord link end
        _net.RegisterNetMessage<RMCPatronListMsg>();
        _net.RegisterNetMessage<RMCClearGhostColorMsg>(OnClearGhostColor);
        _net.RegisterNetMessage<RMCChangeGhostColorMsg>(OnChangeGhostColor);
        _net.RegisterNetMessage<RMCChangeLobbyMessageMsg>(OnChangeLobbyMessage);
        _net.RegisterNetMessage<RMCChangeNTShoutoutMsg>(OnChangeNTShoutout);
        // arcane discord link start
        _serverNet.Connecting += OnConnecting;
        _db.SubscribeToNotifications(OnSponsorUpdated);
        // arcane discord link end
        _userDb.AddOnLoadPlayer(LoadData);
        _userDb.AddOnFinishLoad(FinishLoad);
        _userDb.AddOnPlayerDisconnect(ClientDisconnected);
    }
}
