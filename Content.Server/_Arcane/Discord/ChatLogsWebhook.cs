using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Discord;
using Content.Shared._Arcane.CCVars;
using Content.Shared.Chat;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._Arcane.Discord;

public sealed partial class ChatLogsWebhook : IPostInjectInit
{
    [Dependency] private readonly DiscordWebhook _discord = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _ent = default!;

    private ISawmill _sawmill = default!;

    private Dictionary<ChatChannel, CVarDef<string>> _webhooks = new()
    {
        {ChatChannel.AdminChat, ACCVars.DiscordAdminChatWebhook},
        {ChatChannel.Dead, ACCVars.DiscordDeadChatWebhook},
        {ChatChannel.LOOC, ACCVars.DiscordLOOCChatWebhook},
        {ChatChannel.OOC, ACCVars.DiscordOOCChatWebhook}
    };

    public void PostInject() { }

    public async void CreateChatWebhookMessage(ChatChannel chat, string message, ICommonSession sender)
    {
        if (!_webhooks.TryGetValue(chat, out var webhookCvar))
            return;

        var webhook = _cfg.GetCVar(webhookCvar);
        if (string.IsNullOrEmpty(webhook))
            return;

        message = message.Replace("`", "");

        var senderName = sender.Name;
        if (sender.AttachedEntity != null)
            senderName = _ent.GetComponent<MetaDataComponent>(sender.AttachedEntity.Value).EntityName;

        var payload = new WebhookPayload
        {
            Content = $"{chat}: {senderName} ({sender.Name}): `{message}`"
        };

        await Task.Run(async () => SendWebhook(payload, webhook));
    }

    private async Task SendWebhook(WebhookPayload payload, string url)
    {
        try
        {
            if (await _discord.GetWebhook(url) is not { } identifier)
                return;

            await _discord.CreateMessage(identifier.ToIdentifier(), payload);
        }
        catch (Exception e)
        {
            _sawmill = Logger.GetSawmill("discord");
            _sawmill.Error($"Error while sending vote webhook to Discord: {e}");
        }
    }
}
