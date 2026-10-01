using Robust.Shared.Configuration;

namespace Content.Shared._Arcane.CCVars;

public sealed partial class ACCVars
{
    /// <summary>
    ///     Ahelp history requests are accounted for in periods of this size (seconds).
    /// </summary>
    public static readonly CVarDef<float> AhelpHistoryRateLimitPeriod =
        CVarDef.Create("ahelp.history_rate_limit_period", 5f, CVar.SERVERONLY);

    /// <summary>
    ///     How many Ahelp history requests are allowed in a single rate limit period.
    /// </summary>
    public static readonly CVarDef<int> AhelpHistoryRateLimitCount =
        CVarDef.Create("ahelp.history_rate_limit_count", 2, CVar.SERVERONLY);
}
