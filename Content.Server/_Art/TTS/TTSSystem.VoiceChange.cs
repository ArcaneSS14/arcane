using System.Linq;
using Content.Shared._Art.TTS;
using Content.Shared.Actions;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Art.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem
{
    private static readonly ProtoId<TTSVoicePrototype>[] AvailableVoices = // Hardcoded
    [
        "Adventure_core",
        "Fact_core",
        "Space_core",
        "Glados"
    ];

    private void OnVoiceChangeMenu(TTSVoiceChangeOpenMenuEvent args)
    {
        args.Handled = true;

        if (!TryComp<ActorComponent>(args.Performer, out var actor))
            return;

        RaiseNetworkEvent(new TTSVoiceChangeMenuMessage(AvailableVoices.ToList()), actor.PlayerSession);
    }

    private void OnVoiceChangeSelected(TTSVoiceChangeSelectedMessage ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;

        if (!AvailableVoices.Contains(ev.Voice))
            return;

        if (!TryComp<TTSComponent>(uid, out var component))
            return;

        component.VoicePrototype = ev.Voice;
        Dirty(uid, component);
    }
}
