using Content.Shared._Arcane.TTS;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Prototypes;

namespace Content.Client._Arcane.TTS;

[UsedImplicitly]
public sealed class TTSVoiceChangeUIController : UIController
{
    [UISystemDependency] private readonly TTSVoiceChangeSystem _voiceChangeSystem = default!;

    private TTSVoiceChangeWindow _voiceChangeWindow = default!;

    public void OpenWindow(List<ProtoId<TTSVoicePrototype>> voices)
    {
        EnsureWindow();

        _voiceChangeWindow.PopulateVoices(voices);
        _voiceChangeWindow.VoiceSelected = null;
        _voiceChangeWindow.VoiceSelected += voice =>
        {
            _voiceChangeSystem.SendSelectedVoice(voice);
            _voiceChangeWindow.Close();
        };

        _voiceChangeWindow.OpenCentered();
        _voiceChangeWindow.MoveToFront();
    }

    public void CloseWindow()
    {
        if (_voiceChangeWindow is { Disposed: false })
            _voiceChangeWindow.Close();
    }

    private void EnsureWindow()
    {
        if (_voiceChangeWindow is { Disposed: false })
            return;

        _voiceChangeWindow = UIManager.CreateWindow<TTSVoiceChangeWindow>();
    }
}
