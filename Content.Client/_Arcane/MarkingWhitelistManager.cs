using Content.Shared.Humanoid.Markings;
using Robust.Shared.Player;

namespace Content.Shared.SpecialWhitelist;

public sealed class MarkingWhitelistManager
{
    public static bool IsMarkingAllowed(MarkingPrototype marking, ICommonSession? session)
    {
        // Если вайтлиста нет — разметка доступна всем
        if (marking.Whitelist == null || marking.Whitelist.Allowed.Count == 0)
            return true;

        // Если сессии нет (например, локальный просмотр без игрока), скрываем на всякий случай
        if (session == null)
            return false;

        // Для привязки к аккаунту используем login username, а не отображаемое имя персонажа.
        var ckey = session.Data.UserName;

        foreach (var allowedCkey in marking.Whitelist.Allowed)
        {
            if (string.Equals(allowedCkey, ckey, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
