using Content.Shared._Arcane.TTS;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Mech.Components;

namespace Content.Server._Arcane.Mech;

/// <summary>
///     Applies the mech's configured TTS effect to the pilot's local speech while they are inside, keeping the pilot's
///     own voice. Radio speech is intentionally left untouched: on the frequency the pilot is heard normally.
/// </summary>
public sealed class MechVoiceSystem : EntitySystem
{
    // Tracks the pilot's original TTS effect while they are inside a mech so it can be restored on exit.
    private readonly Dictionary<EntityUid, (string? Effect, bool HadTts)> _savedEffect = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechPilotComponent, TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<MechPilotComponent, ComponentShutdown>(OnPilotShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnTransformSpeech(EntityUid uid, MechPilotComponent pilot, ref TransformSpeechEvent args)
    {
        if (!TryComp<MechVoiceComponent>(pilot.Mech, out var mechVoice) || mechVoice.Effect is not { } effect)
            return;

        var hadTts = TryComp<TTSComponent>(uid, out var tts);
        if (!hadTts)
            tts = AddComp<TTSComponent>(uid);

        // Save the pilot's original effect once, before we override it.
        if (!_savedEffect.ContainsKey(uid))
            _savedEffect[uid] = (tts!.Effect, hadTts);

        if (tts!.Effect != effect)
        {
            tts.Effect = effect;
            Dirty(uid, tts);
        }
    }

    private void OnPilotShutdown(EntityUid uid, MechPilotComponent pilot, ComponentShutdown args)
    {
        // Restore the pilot's original TTS effect now that they are no longer inside the mech.
        if (!_savedEffect.TryGetValue(uid, out var saved))
            return;

        if (saved.HadTts)
        {
            if (TryComp<TTSComponent>(uid, out var tts) && tts.Effect != saved.Effect)
            {
                tts.Effect = saved.Effect;
                Dirty(uid, tts);
            }
        }
        else
        {
            RemComp<TTSComponent>(uid);
        }

        _savedEffect.Remove(uid);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _savedEffect.Clear();
    }
}
