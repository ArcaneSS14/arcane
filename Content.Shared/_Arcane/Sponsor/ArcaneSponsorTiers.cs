namespace Content.Shared._Arcane.Sponsor;

// TODO: избавиться от этой хуйни
public static class ArcaneSponsorTiers
{
    public const string Tier1 = "Tier1";
    public const string Tier2 = "Tier2";
    public const string Tier1OocColor = "#8b00d1";
    public const string Tier2OocColor = "#ecad00";
    public const int TokenMultiplierTier1 = 2;
    public const int TokenMultiplierTier2 = 3;
    public const string UpdatedNotificationChannel = "arcane_sponsor_updated";

    public static bool HasAllRoles(string? tier)
    {
        return tier == Tier2;
    }

    public static bool HasTier(string? currentTier, string? requiredTier)
    {
        if (currentTier == null)
            return false;

        return requiredTier == null ||
               currentTier == requiredTier ||
               HasAllRoles(currentTier);
    }

    public static string GetOocColor(string? tier)
    {
        return tier switch
        {
            Tier2 => Tier2OocColor,
            Tier1 => Tier1OocColor,
            _ => Tier1OocColor,
        };
    }

    public static int GetTokenMultiplier(string? tier)
    {
        return tier switch
        {
            Tier2 => TokenMultiplierTier2,
            Tier1 => TokenMultiplierTier1,
            _ => 1,
        };
    }
}
