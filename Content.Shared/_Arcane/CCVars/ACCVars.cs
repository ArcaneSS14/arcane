using Robust.Shared.Configuration;

namespace Content.Shared._Arcane.CCVars;

[CVarDefs]
public sealed partial class ACCVars
{
    /// <summary>
    ///     Включены ли автоматические голосования в конце раунда.
    /// </summary>
    public static readonly CVarDef<bool> AutoVotingEnabled =
        CVarDef.Create("vote.auto_voting_enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     На каком расстоянии от игрока NPC будет замораживаться.
    /// </summary>
    public static readonly CVarDef<int> NpcSleepRange =
        CVarDef.Create("npc.sleep_range", 30, CVar.SERVERONLY);

    /// <summary>
    ///     Максимальное количество infinity dorms, которые может создать один пользователь.
    /// </summary>
    public static readonly CVarDef<int> MaxUserInfinityDorms =
        CVarDef.Create("infinity_dorms.max_per_user", 2, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     Должна ли система автоматически добавлять пацифист после конца раунда.
    /// </summary>
    public static readonly CVarDef<bool> EndRoundPacification =
        CVarDef.Create("game.end_round_pacifism", true, CVar.SERVERONLY | CVar.ARCHIVE);
}
