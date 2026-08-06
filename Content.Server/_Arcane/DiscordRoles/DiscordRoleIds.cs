using Content.Shared._Arcane.DiscordRoles;

namespace Content.Server._Arcane.DiscordRoles;

/// <summary>
///     Maps game-facing roles to deployment Discord role IDs.
/// </summary>
public static class DiscordRoleIds
{
    public static readonly IReadOnlyDictionary<DiscordRole, ulong> All =
        new Dictionary<DiscordRole, ulong>
        {
            [DiscordRole.SponsorTier1] = 1510991486399942707,
            [DiscordRole.SponsorTier2] = 1510991694785675397,
            [DiscordRole.UnlockRoles] = 1533054913318223872,
        };
}
