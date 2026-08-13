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

    private readonly Dictionary<ICommonSession, EntityUid> _pendingVoiceChange = new();

    private void OnVoiceChangeMenu(TTSVoiceChangeOpenMenuEvent args)
    {
        args.Handled = true;

        var performer = args.Performer;

        if (!TryComp<ActorComponent>(performer, out var actor))
            return;

        var session = actor.PlayerSession;
        _pendingVoiceChange[session] = performer;

        RaiseNetworkEvent(new TTSVoiceChangeMenuMessage(AvailableVoices.ToList()), session);
    }

    private void OnVoiceChangeSelected(TTSVoiceChangeSelectedMessage ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        // Only accept a selection if this session opened the menu for the entity it is attached to.
        if (!_pendingVoiceChange.Remove(session, out var uid))
            return;

        if (session.AttachedEntity != uid)
            return;

        if (!AvailableVoices.Contains(ev.Voice))
            return;

        if (!TryComp<TTSComponent>(uid, out var component))
            return;

        component.VoicePrototype = ev.Voice;
        Dirty(uid, component);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        _pendingVoiceChange.Remove(ev.Player);
    }
}
