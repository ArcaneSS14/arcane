using Robust.Shared.Configuration;

namespace Content.Shared._Arcane.CCVars;

public sealed partial class ACCVars
{
    /// <summary>
    ///     Вебхук для игровых наказаний.
    /// </summary>
    public static readonly CVarDef<string> DiscordBanWebhook =
        CVarDef.Create("discord.ban_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     Вебхук, реплицирующий админ-чат.
    /// </summary>
    public static readonly CVarDef<string> DiscordAdminChatWebhook =
        CVarDef.Create("discord.admin_chat_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     Вебхук, реплицирующий чат мёртвых.
    /// </summary>
    public static readonly CVarDef<string> DiscordDeadChatWebhook =
        CVarDef.Create("discord.dead_chat_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     Вебхук, реплицирующий чат LOOC.
    /// </summary>
    public static readonly CVarDef<string> DiscordLOOCChatWebhook =
        CVarDef.Create("discord.looc_chat_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     Вебхук, реплицирующий чат OOC.
    /// </summary>
    public static readonly CVarDef<string> DiscordOOCChatWebhook =
        CVarDef.Create("discord.ooc_chat_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);
}
