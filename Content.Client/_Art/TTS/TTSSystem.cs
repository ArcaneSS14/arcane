using Content.Shared._Arcane.CCVars;
using Content.Shared._Art.CVars;
using Content.Shared._Art.TTS;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._Art.TTS;

/// <summary>
/// Plays TTS audio in world
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    private ISawmill _sawmill = default!;
    private readonly MemoryContentRoot _contentRoot = new();
    private ResPath _prefix;

    /// <summary>
    /// Volume reduction for whispered TTS (converted to logarithmic scale)
    /// </summary>
    private const float WhisperVolumeReduction = 4f;

    private float _volume = 0.0f;
    // Arcane-start
    private float _radioVolume = 0.0f;
    private bool _useTTS = false;
    // Arcane-end
    private ulong _fileIdx = 0;
    private static ulong _shareIdx = 0;

    public override void Initialize()
    {
        _prefix = ResPath.Root / $"TTS{_shareIdx++}";
        _sawmill = Logger.GetSawmill("tts");
        _res.AddRoot(_prefix, _contentRoot);
        _cfg.OnValueChanged(ArtCVars.TTSVolume, OnTtsVolumeChanged, true);
        // Arcane-start
        _cfg.OnValueChanged(ACCVars.TTSRadioVolume, OnTtsRadioVolumeChanged, true);
        _cfg.OnValueChanged(ACCVars.UseTTS, OnUseTTSChanged, true);
        // Arcane-end
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _contentRoot.Clear();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(ArtCVars.TTSVolume, OnTtsVolumeChanged);
        // Arcane-start
        _cfg.UnsubValueChanged(ACCVars.TTSRadioVolume, OnTtsRadioVolumeChanged);
        _cfg.UnsubValueChanged(ACCVars.UseTTS, OnUseTTSChanged);
        // Arcane-end
        _contentRoot.Clear();
        _contentRoot.Dispose();
    }

    public void RequestGlobalTTS(VoiceRequestType text, string voiceId)
    {
        RaiseNetworkEvent(new RequestPreviewTTSEvent(voiceId));
    }

    private void OnTtsVolumeChanged(float volume)
    {
        _volume = volume;
    }

    // Arcane-start
    private void OnTtsRadioVolumeChanged(float volume)
    {
        _radioVolume = volume;
    }

    private void OnUseTTSChanged(bool value)
    {
        _useTTS = value;
    }
    // Arcane-end

    private void OnPlayTTS(PlayTTSEvent ev)
    {
        // Arcane-start
        if (!_useTTS)
            return;
        // Arcane-end

        _sawmill.Verbose($"Play TTS audio {ev.Data.Length} bytes from {ev.SourceUid} entity");

        var filePath = new ResPath($"{_fileIdx++}.ogg");
        _contentRoot.AddOrUpdateFile(filePath, ev.Data);

        try
        {
            var audioResource = new AudioResource();
            audioResource.Load(IoCManager.Instance!, _prefix / filePath);

            var audioParams = AudioParams.Default
                .WithVolume(AdjustVolume(ev.IsWhisper, ev.SourceUid == null)) // Arcane
                .WithMaxDistance(AdjustDistance(ev.IsWhisper));

            if (ev.SourceUid != null)
            {
                var sourceUid = GetEntity(ev.SourceUid.Value);
                if (sourceUid.IsValid())
                    _audio.PlayEntity(audioResource.AudioStream, sourceUid, null, audioParams);
            }
            else
            {
                _audio.PlayGlobal(audioResource.AudioStream, null, audioParams);
            }
        }
        finally
        {
            _contentRoot.RemoveFile(filePath);
        }
    }

    private float AdjustVolume(bool isWhisper, bool isRadio = false) // Arcane
    {
        var volume = SharedAudioSystem.GainToVolume(_volume);

        if (isWhisper)
            volume -= SharedAudioSystem.GainToVolume(WhisperVolumeReduction);

        // Arcane-start
        if (isRadio)
            volume = SharedAudioSystem.GainToVolume(_radioVolume);
        // Arcane-end

        return volume;
    }

    private float AdjustDistance(bool isWhisper)
    {
        return isWhisper ? SharedChatSystem.WhisperMuffledRange : SharedChatSystem.VoiceRange;
    }
}
