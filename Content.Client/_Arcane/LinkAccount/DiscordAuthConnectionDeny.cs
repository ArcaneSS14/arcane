using Robust.Shared.Network;

namespace Content.Client._Arcane.LinkAccount;

public readonly record struct DiscordAuthConnectionDenyInfo(
    bool LinkRequired,
    bool PlayerRoleRequired,
    string? Code,
    string? InviteLink)
{
    public bool Visible => LinkRequired || PlayerRoleRequired || !string.IsNullOrEmpty(Code);
}

public static class DiscordAuthConnectionDeny
{
    private const string DiscordLinkRequiredKey = "discord_link_required";
    private const string DiscordPlayerRoleRequiredKey = "discord_player_role_required";
    private const string DiscordLinkCodeKey = "discord_link_code";
    private const string DiscordInviteLinkKey = "discord_invite_link";

    public static DiscordAuthConnectionDenyInfo FromReason(INetStructuredReason? reason, string? fallbackReason = null)
    {
        var code = reason?.Message.StringOf(DiscordLinkCodeKey);
        var inviteLink = reason?.Message.StringOf(DiscordInviteLinkKey);

        fallbackReason ??= reason?.Reason;

        if (string.IsNullOrEmpty(code))
            code = ExtractCode(fallbackReason);

        if (string.IsNullOrEmpty(inviteLink))
            inviteLink = ExtractInviteLink(fallbackReason);

        return new DiscordAuthConnectionDenyInfo(
            reason?.Message.BoolOf(DiscordLinkRequiredKey, false) == true,
            reason?.Message.BoolOf(DiscordPlayerRoleRequiredKey, false) == true,
            code,
            inviteLink);
    }

    private static string? ExtractCode(string? reason)
    {
        if (string.IsNullOrEmpty(reason))
            return null;

        var parts = reason.Split(
            [' ', '\r', '\n', '\t', ':', ';', ',', '.', '(', ')', '[', ']', '{', '}', '"', '\''],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (Guid.TryParse(part, out var code))
                return code.ToString();
        }

        return null;
    }

    private static string? ExtractInviteLink(string? reason)
    {
        if (string.IsNullOrEmpty(reason))
            return null;

        var parts = reason.Split(
            [' ', '\r', '\n', '\t', '<', '>', '"', '\''],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var link = part.TrimEnd('.', ',', ';', ')', ']');
            if (link.StartsWith("https://discord.gg/", StringComparison.OrdinalIgnoreCase) ||
                link.StartsWith("http://discord.gg/", StringComparison.OrdinalIgnoreCase))
            {
                return link;
            }

            if (link.StartsWith("https://discord.com/invite/", StringComparison.OrdinalIgnoreCase) ||
                link.StartsWith("http://discord.com/invite/", StringComparison.OrdinalIgnoreCase))
            {
                return link;
            }
        }

        return null;
    }
}
