using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Content.Shared._Arcane.CCVars; // Arcane
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Components; // Arcane
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration; // Arcane
using Robust.Shared.Network; // Arcane

namespace Content.Goobstation.Shared.StationRadio.Systems;

public sealed class StationRadioReceiverSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    // Arcane-Start
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    // Arcane-End

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaPlayedEvent>(OnMediaPlayed);
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaStoppedEvent>(OnMediaStopped);
        SubscribeLocalEvent<StationRadioReceiverComponent, ActivateInWorldEvent>(OnRadioToggle);
        SubscribeLocalEvent<StationRadioReceiverComponent, PowerChangedEvent>(OnPowerChanged);
        // Arcane-Start
        // The media is played by the server, so the listener's own volume has to be reapplied
        // whenever the receiver state or the cvar changes.
        SubscribeLocalEvent<StationRadioReceiverComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
        if (_net.IsClient)
            _cfg.OnValueChanged(ACCVars.StationRadioVolume, _ => ApplyPersonalVolume());
        // Arcane-End
    }

    private void OnPowerChanged(EntityUid uid, StationRadioReceiverComponent comp, PowerChangedEvent args)
    {
        // Arcane-Start
        ApplyVolume((uid, comp));
        // Arcane-End
    }

    private void OnRadioToggle(EntityUid uid, StationRadioReceiverComponent comp, ActivateInWorldEvent args)
    {
        comp.Active = !comp.Active;
        // Arcane-Start
        ApplyVolume((uid, comp));
        // Arcane-End
    }

    private void OnMediaPlayed(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaPlayedEvent args)
    {
        var audio = _audio.PlayPredicted(args.MediaPlayed, uid, uid, comp.DefaultParams);
        // Arcane-Start
        if (audio == null)
            return;

        comp.SoundEntity = audio.Value.Entity;
        ApplyVolume((uid, comp));
        // Arcane-End
    }

    private void OnMediaStopped(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaStoppedEvent args)
    {
        if (comp.SoundEntity == null)
            return;

        comp.SoundEntity = _audio.Stop(comp.SoundEntity);
    }

    // Arcane-Start
    private void OnAfterHandleState(Entity<StationRadioReceiverComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplyVolume(ent);
    }

    private void ApplyPersonalVolume()
    {
        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out _))
            ApplyVolume(receiver);
    }

    /// <summary>
    ///     Sets the volume of the media the radio is playing, keeping in mind the radio state and the
    ///     listener's own volume. The personal volume is a client-only cvar, so the server sticks to the
    ///     volume the media was played with.
    /// </summary>
    private void ApplyVolume(Entity<StationRadioReceiverComponent> ent)
    {
        if (ent.Comp.SoundEntity is not { } sound || !TryComp<AudioComponent>(sound, out var audio))
            return;

        var volume = _power.IsPowered(ent) && ent.Comp.Active
            ? ent.Comp.DefaultParams.Volume + GetPersonalOffset()
            : float.NegativeInfinity;

        // Leave the media as the server played it unless our own volume actually changes something,
        // so that the client volume isn't reverted by the next state the server sends.
        if (audio.Params.Volume.Equals(volume))
            return;

        _audio.SetVolume(sound, volume);
    }

    private float GetPersonalOffset()
    {
        if (!_net.IsClient)
            return 0f;

        var gain = _cfg.GetCVar(ACCVars.StationRadioVolume);
        return gain <= 0.01f ? float.NegativeInfinity : SharedAudioSystem.GainToVolume(gain);
    }
    // Arcane-End
}
