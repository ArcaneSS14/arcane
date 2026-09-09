using Robust.Shared.Configuration;

namespace Content.Shared._Arcane.CCVars;

public sealed partial class ACCVars
{
    /// <summary>
    ///     Should the client use TTS instead of barks.
    /// </summary>
    public static readonly CVarDef<bool> UseTTS =
        CVarDef.Create("tts.use_tts", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     TTS radio volume.
    /// </summary>
    public static readonly CVarDef<float> TTSRadioVolume =
        CVarDef.Create("tts.radio_volume", 0.5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Frequencies of radio channels on which TTS radio is disabled, separated by ';'.
    /// </summary>
    public static readonly CVarDef<string> TTSRadioChannelMuted =
        CVarDef.Create("tts.radio_channel_muted", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Volumes of TTS on radio channels: "frequency=multiplier;...", multiplier relative to <see cref="TTSRadioVolume"/>.
    /// </summary>
    public static readonly CVarDef<string> TTSRadioChannelVolumes =
        CVarDef.Create("tts.radio_channel_volumes", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Whether radio transmissions are read out loud with TTS while the local player is a ghost.
    ///     Defaults to enabled; reset back to true whenever the player stops being a ghost.
    /// </summary>
    public static readonly CVarDef<bool> TTSGhostRadioUseTTS =
        CVarDef.Create("tts.ghost_radio_use_tts", true, CVar.CLIENTONLY);
}
