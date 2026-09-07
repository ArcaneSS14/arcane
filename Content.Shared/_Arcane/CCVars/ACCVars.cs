using Robust.Shared.Configuration;

namespace Content.Shared._Arcane.CCVars;

[CVarDefs]
public sealed partial class ACCVars
{
    /// <summary>
    ///     Are auto voting enabled at the end of a round?
    /// </summary>
    public static readonly CVarDef<bool> AutoVotingEnabled =
        CVarDef.Create("vote.auto_voting_enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     At what distance from the player the NPC will sleep.
    /// </summary>
    public static readonly CVarDef<int> NpcSleepRange =
        CVarDef.Create("npc.sleep_range", 40, CVar.SERVERONLY);

    /// <summary>
    ///     The maximum number of infinity dorms that one user can create.
    /// </summary>
    public static readonly CVarDef<int> MaxUserInfinityDorms =
        CVarDef.Create("infinity_dorms.max_per_user", 2, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     Should the system automatically add a pacifist after the end of the round?
    /// </summary>
    public static readonly CVarDef<bool> EndRoundPacification =
        CVarDef.Create("game.end_round_pacifism", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Should the server automatically restart after the end of a round?
    /// </summary>
    public static readonly CVarDef<bool> RestartServerOnRoundEnd =
        CVarDef.Create("game.restart_server_on_round_end", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     After how many rounds will the server restart if the auto-restart option is enabled.
    /// </summary>
    public static readonly CVarDef<int> RoundsToRestart =
        CVarDef.Create("game.rounds_to_restart", 8, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Offer item.
    /// </summary>
    public static readonly CVarDef<bool> OfferModeIndicatorsPointShow =
        CVarDef.Create("hud.offer_mode_indicators_point_show", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    ///     Whether the player should pick up items in combat mode or not.
    /// </summary>
    public static readonly CVarDef<bool> CombatModeBlockItemPickup =
        CVarDef.Create("accessibility.combat_mode_block_item_pickup", false, CVar.ARCHIVE);

    /// <summary>
    ///     Swaps the secondary action (RMB) and opening the interaction window (Alt+LMB) in non-combat mode.
    /// </summary>
    public static readonly CVarDef<bool> TG13Controls =
        CVarDef.Create("accessibility.tg13_controls", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Enables alternate job titles for players.
    /// </summary>
    public static readonly CVarDef<bool> ICAlternateJobTitlesEnable =
        CVarDef.Create("ic.alternate_job_titles_enable", true, CVar.SERVER | CVar.REPLICATED);
}
