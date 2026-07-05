using Robust.Shared.Configuration;

namespace Content.Shared._Arcane.CCVars;

public sealed partial class ACCVars
{
    /// <summary>
    ///     Должен ли клиент использовать ТТС вместо барков.
    /// </summary>
    public static readonly CVarDef<bool> UseTTS =
        CVarDef.Create("tts.use_tts", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Громкость ТТСа рации.
    /// </summary>
    public static readonly CVarDef<float> TTSRadioVolume =
        CVarDef.Create("tts.radio_volume", 0.5f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
