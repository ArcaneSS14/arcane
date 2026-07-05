using Robust.Shared.Configuration;

namespace Content.Shared._Arcane.CCVars;

public sealed partial class ACCVars
{
    /// <summary>
    ///     Включена ли автоматическая очистка мусора.
    /// </summary>
    public static readonly CVarDef<bool> AutoCleaningEnabled =
        CVarDef.Create("optimization.auto_cleaning", false, CVar.SERVERONLY | CVar.ARCHIVE);
}
