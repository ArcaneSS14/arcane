namespace Content.Shared._Arcane.DiscordRoles;

/// <summary>
///     Discord roles that may be used by game systems.
/// </summary>
public enum DiscordRole : byte
{
    SponsorTier1,
    SponsorTier2,
    UnlockRoles,
}

public static class DiscordRoleConstants
{
    public const string UpdatedNotificationChannel = "arcane_discord_roles_updated";
}
