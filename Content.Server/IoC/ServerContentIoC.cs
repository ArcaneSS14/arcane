// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Arcane.Discord;
using Content.Server._Arcane.DiscordRoles;
using Content.Server._Arcane.ERP.Preferences;
using Content.Server._Art.TTS;
using Content.Server._Goobstation.Antag;
using Content.Server._Orion.ServerProtection;
using Content.Server._Orion.ServerProtection.Administration;
using Content.Server._Orion.ServerProtection.Chat;
using Content.Server._Orion.ServerProtection.Emoting;
using Content.Server._RMC14.LinkAccount; // RMC - Patreon
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Notes;
using Content.Server.Afk;
using Content.Server.Chat.Managers;
using Content.Server.Connection;
using Content.Server.Database;
using Content.Server.Discord;
using Content.Server.Discord.DiscordLink;
using Content.Server.Discord.WebhookMessages;
using Content.Server.EUI;
using Content.Server.GhostKick;
using Content.Server.Info;
using Content.Server.Mapping;
using Content.Server.Maps;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.Players.JobWhitelist;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Players.RateLimiting;
using Content.Server.Preferences.Managers;
using Content.Server.ServerInfo;
using Content.Server.ServerUpdates;
using Content.Server.Voting.Managers;
using Content.Server.Worldgen.Tools;
using Content.Shared.Administration.Logs;
using Content.Shared.Administration.Managers;
using Content.Shared._Arcane.DiscordRoles;
using Content.Shared.Chat;
using Content.Shared.IoC;
using Content.Shared.Kitchen;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Players.RateLimiting;

namespace Content.Server.IoC;

internal static class ServerContentIoC
{
    public static void Register(IDependencyCollection deps)
    {
        SharedContentIoC.Register(deps);
        deps.Register<IChatManager, ChatManager>();
        deps.Register<ISharedChatManager, ChatManager>();
        deps.Register<IChatSanitizationManager, ChatSanitizationManager>();
        deps.Register<IServerPreferencesManager, ServerPreferencesManager>();
        deps.Register<IServerDbManager, ServerDbManager>();
        deps.Register<RecipeManager, RecipeManager>();
        deps.Register<INodeGroupFactory, NodeGroupFactory>();
        deps.Register<IConnectionManager, ConnectionManager>();
        deps.Register<ServerUpdateManager>();
        deps.Register<IAdminManager, AdminManager>();
        deps.Register<ISharedAdminManager, AdminManager>();
        deps.Register<EuiManager, EuiManager>();
        deps.Register<IVoteManager, VoteManager>();
        deps.Register<IPlayerLocator, PlayerLocator>();
        deps.Register<IAfkManager, AfkManager>();
        deps.Register<IGameMapManager, GameMapManager>();
        deps.Register<RulesManager, RulesManager>();
        deps.Register<IBanManager, BanManager>();
        deps.Register<ContentNetworkResourceManager>();
        deps.Register<IAdminNotesManager, AdminNotesManager>();
        deps.Register<GhostKickManager>();
        deps.Register<ISharedAdminLogManager, AdminLogManager>();
        deps.Register<IAdminLogManager, AdminLogManager>();
        deps.Register<PlayTimeTrackingManager>();
        deps.Register<UserDbDataManager>();
        deps.Register<ServerInfoManager>();
        deps.Register<PoissonDiskSampler>();
        deps.Register<DiscordWebhook>();
        deps.Register<VoteWebhooks>();
        deps.Register<ServerDbEntryManager>();
        deps.Register<ISharedPlaytimeManager, PlayTimeTrackingManager>();
        deps.Register<ServerApi>();
        deps.Register<JobWhitelistManager>();
        deps.Register<PlayerRateLimitManager>();
        deps.Register<SharedPlayerRateLimitManager, PlayerRateLimitManager>();
        deps.Register<MappingManager>();
        deps.Register<IWatchlistWebhookManager, WatchlistWebhookManager>();
        deps.Register<ConnectionManager>();
        deps.Register<MultiServerKickManager>();
        deps.Register<CVarControlManager>();
        deps.Register<DiscordLink>();
        deps.Register<DiscordChatLink>();
        deps.Register<LastRolledAntagManager>(); // Goobstation - antag pity
        deps.Register<LinkAccountManager>(); // RMC - Patreon
        deps.Register<DiscordRoleManager>(); // Arcane
        deps.Register<ISharedDiscordRoleManager, DiscordRoleManager>(); // Arcane
        // Orion-Start
        deps.Register<ServerProtectionAuditManager>();
        deps.Register<ServerProtectionPunishmentSystem>();
        deps.Register<ChatProtectionSystem>();
        deps.Register<EmoteProtectionSystem>();
        deps.Register<AdminActionProtectionSystem>();
        // Orion-End
        deps.Register<ErpOrganPreferencesManager>(); // Arcane
        deps.Register<TTSManager>(); // Art-TTS
        deps.Register<BanWebhooks>(); // Arcane
        deps.Register<ChatLogsWebhook>(); // Arcane
    }
}
