using Robust.Shared.Configuration;

namespace Content.Shared._Arcane.CCVars;

public sealed partial class ACCVars
{
    public static readonly CVarDef<bool> ActionsBarFreePlacement =
        CVarDef.Create("hud.actions_bar_free_placement", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> ActionsBarPositionX =
        CVarDef.Create("hud.actions_bar_position_x", -1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> ActionsBarPositionY =
        CVarDef.Create("hud.actions_bar_position_y", -1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    // Формат: slot:x:y через ';'
    public static readonly CVarDef<string> ActionsBarSlotPositions =
        CVarDef.Create("hud.actions_bar_slot_positions", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);
}
