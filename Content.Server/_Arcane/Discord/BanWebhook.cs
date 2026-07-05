using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Discord;
using Content.Server.GameTicking;
using Content.Shared.Database;
using Robust.Server;
using Robust.Server.Player;
using Robust.Shared.Network;

namespace Content.Server._Arcane.Discord;

public sealed partial class BanWebhooks : IPostInjectInit
{
    [Dependency] private readonly DiscordWebhook _discord = default!;
    [Dependency] private readonly IEntitySystemManager _entSys = default!;
    [Dependency] private readonly IBaseServer _baseServer = default!;


    private ISawmill _sawmill = default!;

    public void PostInject() { }

    public async void CreateBanWebhookMessage(BanWebhookData data, string url)
    {
        _sawmill = Logger.GetSawmill("discord");
        var gameTicker = _entSys.GetEntitySystemOrNull<GameTicker>();

        var serverName = _baseServer.ServerName;
        var runId = gameTicker != null ? gameTicker.RoundId : 0;

        var payload = new WebhookPayload
        {
            Username = Loc.GetString("server-ban-webhook-name"),
            Embeds = new List<WebhookEmbed>
            {
                new()
                {
                    Title = data.Title,
                    Color = data.Color,
                    Description = data.Description,
                    Footer = new WebhookEmbedFooter
                    {
                        Text = Loc.GetString(
                            "ban-webhook-footer",
                            ("serverName", serverName),
                            ("roundId", runId)),
                    }
                },
            },
        };

        await Task.Run(async () => SendBanWebhook(payload, url));
    }

    private async Task SendBanWebhook(WebhookPayload payload, string url)
    {
        try
        {
            if (await _discord.GetWebhook(url) is not { } identifier)
                return;

            _sawmill.Debug(JsonSerializer.Serialize(payload));

            await _discord.CreateMessage(identifier.ToIdentifier(), payload);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while sending vote webhook to Discord: {e}");
        }
    }
}

public sealed class BanWebhookData
{
    public string Title;
    public string Description;
    public int Color;

    public BanWebhookData(IPlayerManager playerManager, NetUserId? playerUser, NetUserId? adminUser, string reason, NoteSeverity severity, DateTimeOffset? expirationTime, IReadOnlyCollection<string>? roles = null)
    {
        var emptyString = Loc.GetString("ban-webhook-empty");

        var player = emptyString;
        var admin = emptyString;
        var roleBans = string.Empty;
        var expiration = Loc.GetString("ban-webhook-empty-expiration");

        if (playerUser != null)
            player = playerManager.GetPlayerData(playerUser.Value).UserName;

        if (adminUser != null)
            admin = playerManager.GetPlayerData(adminUser.Value).UserName;

        if (expirationTime != null)
            expiration = $"<t:{expirationTime.Value.ToUnixTimeSeconds()}:f>";

        if (roles != null)
        {
            foreach (var role in roles)
            {
                roleBans += $"- {role}\n";
            }
        }

        Title = Loc.GetString(roles != null ? "role-ban-webhook-title" : "server-ban-webhook-title");

        Description = Loc.GetString(
                        roles != null ? "role-ban-webhook-description" : "server-ban-webhook-description",
                        ("player", player),
                        ("admin", admin),
                        ("expiration", expiration),
                        ("severity", severity),
                        ("reason", reason),
                        ("roles", roles != null ? roleBans : string.Empty));

        Color = _severityColors.GetValueOrDefault(severity, 42198);
    }

    private Dictionary<NoteSeverity, int> _severityColors = new()
    {
        {NoteSeverity.High, 12198144},
        {NoteSeverity.Medium, 12207872},
        {NoteSeverity.Minor, 14072832},
        {NoteSeverity.None, 42198},
    };
}
