using Robust.Shared.Configuration;

namespace Content.Shared._Arcane.CCVars;

public sealed partial class ACCVars
{
    /// <summary>
    /// Custom X position of the actions bar in virtual UI pixels. A negative value means the default screen layout is used.
    /// </summary>
    public static readonly CVarDef<float> ActionsBarPositionX =
        CVarDef.Create("hud.actions_bar_position_x", -1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Custom Y position of the actions bar in virtual UI pixels. A negative value means the default screen layout is used.
    /// </summary>
    public static readonly CVarDef<float> ActionsBarPositionY =
        CVarDef.Create("hud.actions_bar_position_y", -1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Custom positions of individual hotbar slots detached from the actions bar grid,
    /// serialized as <c>slot:x:y</c> entries separated by <c>;</c>. Slot indices match the hotbar keys.
    /// </summary>
    public static readonly CVarDef<string> ActionsBarSlotPositions =
        CVarDef.Create("hud.actions_bar_slot_positions", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);
}
