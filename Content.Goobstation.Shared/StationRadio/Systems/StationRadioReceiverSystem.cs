using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Goobstation.Shared.StationRadio.Systems;

public sealed class StationRadioReceiverSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!; // Arcane-Edit

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaPlayedEvent>(OnMediaPlayed);
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaStoppedEvent>(OnMediaStopped);
        SubscribeLocalEvent<StationRadioReceiverComponent, ActivateInWorldEvent>(OnRadioToggle);
    }

    private void OnRadioToggle(EntityUid uid, StationRadioReceiverComponent comp, ActivateInWorldEvent args)
    {
        comp.Active = !comp.Active;
        Dirty(uid, comp); // Arcane-Edit
    }

    private void OnMediaPlayed(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaPlayedEvent args)
    {
        // Arcane-Edit-Start
        comp.CurrentMedia = _audio.ResolveSound(args.MediaPlayed);
        comp.MediaStartTime = _timing.CurTime;
        comp.PlaybackId++;
        Dirty(uid, comp);
        // Arcane-Edit-End
    }

    private void OnMediaStopped(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaStoppedEvent args)
    {
        if (comp.CurrentMedia == null) // Arcane-Edit
            return;

        // Arcane-Edit-Start
        comp.CurrentMedia = null;
        comp.MediaStartTime = null;
        Dirty(uid, comp);
        // Arcane-Edit-End
    }

    // Arcane-Edit-Start
    public static float ComputeVolumeForRadio(float defaultVolume, float personalMultiplier, bool powered, bool active)
    {
        if (!powered || !active)
            return float.NegativeInfinity;

        var gain = personalMultiplier <= 0.01f
            ? float.NegativeInfinity
            : SharedAudioSystem.GainToVolume(personalMultiplier);

        return defaultVolume + gain;
    }
    // Arcane-Edit-End
}
