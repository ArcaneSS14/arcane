using Content.Shared._Art.TTS;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Art.TTS;

public sealed class TTSVoiceChangeSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;

    private TTSVoiceChangeUIController? _controller;

    public override void Initialize()
    {
        SubscribeNetworkEvent<TTSVoiceChangeMenuMessage>(OnOpenMenu);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnOpenMenu(TTSVoiceChangeMenuMessage ev)
    {
        if (ev.Voices.Count == 0)
            return;

        _controller ??= _userInterfaceManager.GetUIController<TTSVoiceChangeUIController>();
        _controller.OpenWindow(ev.Voices);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _controller?.CloseWindow();
    }

    public void SendSelectedVoice(ProtoId<TTSVoicePrototype> voice)
    {
        RaiseNetworkEvent(new TTSVoiceChangeSelectedMessage(voice));
    }
}
