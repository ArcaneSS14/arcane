using Content.Shared._Arcane.DiscordRoles;
using Robust.Shared.Player;

namespace Content.Shared._Arcane.Sponsor;

public sealed record SponsorRoleBenefit(
    string TierName,
    string OocColor,
    int TokenMultiplier,
    int CreditsPriority);

public static class SponsorRoleBenefits
{
    public static readonly IReadOnlyDictionary<DiscordRole, SponsorRoleBenefit> All =
        new Dictionary<DiscordRole, SponsorRoleBenefit>
        {
            [DiscordRole.SponsorTier1] = new("Tier1", "#8b00d1", 2, -1),
            [DiscordRole.SponsorTier2] = new("Tier2", "#ecad00", 3, -2),
        };

    private static readonly DiscordRole[] Priority =
    [
        DiscordRole.SponsorTier2,
        DiscordRole.SponsorTier1,
    ];

    public static bool TryGetHighestRole(
        ISharedDiscordRoleManager roles,
        ICommonSession session,
        out DiscordRole role)
    {
        foreach (var candidate in Priority)
        {
            if (!roles.HasRole(session, candidate))
                continue;

            role = candidate;
            return true;
        }

        role = default;
        return false;
    }

    public static bool TryGetRole(string tierName, out DiscordRole role)
    {
        foreach (var (candidate, benefit) in All)
        {
            if (!string.Equals(tierName, benefit.TierName, StringComparison.Ordinal))
                continue;

            role = candidate;
            return true;
        }

        role = default;
        return false;
    }

    public static bool TryGetOocColor(
        ISharedDiscordRoleManager roles,
        ICommonSession session,
        out string color)
    {
        if (TryGetHighestRole(roles, session, out var role))
        {
            color = All[role].OocColor;
            return true;
        }

        color = string.Empty;
        return false;
    }

    public static int GetTokenMultiplier(ISharedDiscordRoleManager roles, ICommonSession session)
    {
        return TryGetHighestRole(roles, session, out var role)
            ? All[role].TokenMultiplier
            : 1;
    }
}
