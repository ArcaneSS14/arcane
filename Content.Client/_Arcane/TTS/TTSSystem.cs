using Content.Client.Ghost;
using Content.Shared._Arcane.CCVars;
using Content.Shared._Arcane.CVars;
using Content.Shared._Arcane.TTS;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._Arcane.TTS;

/// <summary>
/// Plays TTS audio in world
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceManager _res = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private GhostSystem _ghostSystem = default!;

    private ISawmill _sawmill = default!;
    private readonly MemoryContentRoot _contentRoot = new();
    private ResPath _prefix;

    /// <summary>
    /// Volume reduction for whispered TTS (converted to logarithmic scale)
    /// </summary>
    private const float WhisperVolumeReduction = 4f;

    private float _volume = 0.0f;
    private float _radioVolume = 0.0f;
    private bool _useTTS = false;
    private bool _ghostRadioUseTTS = false;
    private readonly HashSet<int> _mutedRadioChannels = new();
    private readonly Dictionary<int, float> _radioChannelVolumes = new();
    private ulong _fileIdx = 0;
    private static ulong _shareIdx = 0;

    public override void Initialize()
    {
        _prefix = ResPath.Root / $"TTS{_shareIdx++}";
        _sawmill = Logger.GetSawmill("tts");
        _res.AddRoot(_prefix, _contentRoot);
        _cfg.OnValueChanged(ArtCVars.TTSVolume, OnTtsVolumeChanged, true);
        _cfg.OnValueChanged(ACCVars.TTSRadioVolume, OnTtsRadioVolumeChanged, true);
        _cfg.OnValueChanged(ACCVars.UseTTS, OnUseTTSChanged, true);
        _cfg.OnValueChanged(ACCVars.TTSRadioChannelMuted, OnTTSRadioChannelMutedChanged, true);
        _cfg.OnValueChanged(ACCVars.TTSRadioChannelVolumes, OnTTSRadioChannelVolumesChanged, true);
        _cfg.OnValueChanged(ACCVars.TTSGhostRadioUseTTS, OnGhostRadioUseTTSChanged, true);
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
        _cfg.UnsubValueChanged(ACCVars.TTSRadioVolume, OnTtsRadioVolumeChanged);
        _cfg.UnsubValueChanged(ACCVars.UseTTS, OnUseTTSChanged);
        _cfg.UnsubValueChanged(ACCVars.TTSRadioChannelMuted, OnTTSRadioChannelMutedChanged);
        _cfg.UnsubValueChanged(ACCVars.TTSRadioChannelVolumes, OnTTSRadioChannelVolumesChanged);
        _cfg.UnsubValueChanged(ACCVars.TTSGhostRadioUseTTS, OnGhostRadioUseTTSChanged);
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

    private void OnTtsRadioVolumeChanged(float volume)
    {
        _radioVolume = volume;
    }

    private void OnUseTTSChanged(bool value)
    {
        _useTTS = value;
    }

    private void OnGhostRadioUseTTSChanged(bool value)
    {
        _ghostRadioUseTTS = value;
    }

    private void OnTTSRadioChannelMutedChanged(string value)
    {
        _mutedRadioChannels.Clear();
        _mutedRadioChannels.UnionWith(TTSRadioChannelSettings.ParseMuted(value));
    }

    private void OnTTSRadioChannelVolumesChanged(string value)
    {
        _radioChannelVolumes.Clear();
        foreach (var (frequency, volume) in TTSRadioChannelSettings.ParseVolumes(value))
            _radioChannelVolumes[frequency] = volume;
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
        var isRadio = ev.SourceUid == null && ev.Frequency is { };
        var isGhost = _ghostSystem.IsGhost;

        if (isRadio)
        {
            if (isGhost)
            {
                if (!_ghostRadioUseTTS)
                    return;
            }
            else if (!_useTTS)
            {
                return;
            }
        }
        else if (!_useTTS)
        {
            return;
        }

        if (ev.SourceUid == null && ev.Frequency is { } frequency && _mutedRadioChannels.Contains(frequency))
            return;

        _sawmill.Verbose($"Play TTS audio {ev.Data.Length} bytes from {ev.SourceUid} entity");

        var filePath = new ResPath($"{_fileIdx++}.ogg");
        _contentRoot.AddOrUpdateFile(filePath, ev.Data);

        try
        {
            var audioResource = new AudioResource();
            audioResource.Load(IoCManager.Instance!, _prefix / filePath);

            var audioParams = AudioParams.Default
                .WithVolume(AdjustVolume(ev.IsWhisper, ev.SourceUid == null, ev.Frequency))
                .WithMaxDistance(AdjustDistance(ev.IsWhisper));

            if (ev.SourceUid != null)
            {
                if (!TryGetEntity(ev.SourceUid.Value, out _))
                    return;
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

    private float AdjustVolume(bool isWhisper, bool isRadio = false, int? frequency = null)
    {
        var volume = SharedAudioSystem.GainToVolume(_volume);

        if (isWhisper)
            volume -= SharedAudioSystem.GainToVolume(WhisperVolumeReduction);

        if (isRadio)
        {
            var multiplier = 1f;
            if (frequency is { } freq && _radioChannelVolumes.TryGetValue(freq, out var channelVolume))
                multiplier = channelVolume;

            volume = SharedAudioSystem.GainToVolume(_radioVolume * multiplier);
        }

        return volume;
    }

    private float AdjustDistance(bool isWhisper)
    {
        return isWhisper ? SharedChatSystem.WhisperMuffledRange : SharedChatSystem.VoiceRange;
    }
}
