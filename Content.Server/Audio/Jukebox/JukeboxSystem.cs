// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Audio.Jukebox;
using Content.Shared.Power;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using JukeboxComponent = Content.Shared.Audio.Jukebox.JukeboxComponent;

namespace Content.Server.Audio.Jukebox;


public sealed class JukeboxSystem : SharedJukeboxSystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!; // Orion

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JukeboxComponent, JukeboxSelectedMessage>(OnJukeboxSelected);
        SubscribeLocalEvent<JukeboxComponent, JukeboxPlayingMessage>(OnJukeboxPlay);
        SubscribeLocalEvent<JukeboxComponent, JukeboxPauseMessage>(OnJukeboxPause);
        SubscribeLocalEvent<JukeboxComponent, JukeboxStopMessage>(OnJukeboxStop);
        SubscribeLocalEvent<JukeboxComponent, JukeboxSetTimeMessage>(OnJukeboxSetTime);
        SubscribeLocalEvent<JukeboxComponent, JukeboxSetVolumeMessage>(OnJukeboxSetVolume); // Orion
        SubscribeLocalEvent<JukeboxComponent, JukeboxToggleLoopMessage>(OnJukeboxToggleLoop); // Orion
        SubscribeLocalEvent<JukeboxComponent, ComponentInit>(OnComponentInit);
        // SubscribeLocalEvent<JukeboxComponent, ComponentShutdown>(OnComponentShutdown); // Arcane-Edit

        SubscribeLocalEvent<JukeboxComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnComponentInit(EntityUid uid, JukeboxComponent component, ComponentInit args)
    {
        if (HasComp<ApcPowerReceiverComponent>(uid))
        {
            TryUpdateVisualState(uid, component);
        }
    }

    private void OnJukeboxPlay(EntityUid uid, JukeboxComponent component, ref JukeboxPlayingMessage args)
    {
        // Arcane-Edit-Start
        if (component.Active && component.Playing)
            return;

        if (!component.Active)
        {
            if (string.IsNullOrEmpty(component.SelectedSongId))
                return;

            component.Active = true;
            component.CurrentPlaybackOffset = 0f;
        }

        component.Playing = true;
        component.PlaybackStartTime = _gameTiming.CurTime;
        // Arcane-Edit-End
        Dirty(uid, component);
    }

    private void OnJukeboxPause(Entity<JukeboxComponent> ent, ref JukeboxPauseMessage args)
    {
        // Arcane-Edit-Start
        if (!ent.Comp.Active || !ent.Comp.Playing)
            return;

        // Orion-Start
        if (ent.Comp.PlaybackStartTime is { } start)
        {
            ent.Comp.CurrentPlaybackOffset += (float) (_gameTiming.CurTime - start).TotalSeconds;
            ent.Comp.PlaybackStartTime = null;
        }

        ent.Comp.Playing = false;
        // Orion-End
        Dirty(ent);
        // Arcane-Edit-End
    }

    private void OnJukeboxSetTime(EntityUid uid, JukeboxComponent component, JukeboxSetTimeMessage args)
    {
        // Arcane-Edit-Start
        if (!TryComp(args.Actor, out ActorComponent? actorComp))
            return;

        var offset = actorComp.PlayerSession.Channel.Ping * 1.5f / 1000f;
        // Orion-Start
        component.CurrentPlaybackOffset = MathF.Max(0f, args.SongTime + offset);
        component.PlaybackStartTime = _gameTiming.CurTime;
        // Orion-End
        Dirty(uid, component);
        // Arcane-Edit-End
    }

    // Orion-Start
    private void OnJukeboxSetVolume(EntityUid uid, JukeboxComponent component, JukeboxSetVolumeMessage args)
    {
        SetJukeboxVolume(uid, component, args.Volume);
    }

    private void OnJukeboxToggleLoop(EntityUid uid, JukeboxComponent component, JukeboxToggleLoopMessage args)
    {
        ToggleLoop(uid, component);
    }
    // Orion-End

    private void OnPowerChanged(Entity<JukeboxComponent> entity, ref PowerChangedEvent args)
    {
        TryUpdateVisualState(entity);

        if (!this.IsPowered(entity.Owner, EntityManager))
        {
            Stop(entity);
        }
    }

    private void OnJukeboxStop(Entity<JukeboxComponent> entity, ref JukeboxStopMessage args)
    {
        Stop(entity);
    }

    private void Stop(Entity<JukeboxComponent> entity)
    {
        // Audio.SetState(entity.Comp.AudioStream, AudioState.Stopped); // Arcane-Edit
        // Orion-Start
        // Arcane-Start
        entity.Comp.Active = false;
        entity.Comp.Playing = false;
        // Arcane-End
        entity.Comp.CurrentPlaybackOffset = 0f;
        entity.Comp.PlaybackStartTime = null;
        // Orion-End
        Dirty(entity);
    }

    private void OnJukeboxSelected(EntityUid uid, JukeboxComponent component, JukeboxSelectedMessage args)
    {
        // Arcane-Edit-Start
        if (component.Playing)
            return;

        component.SelectedSongId = args.SongId;

        // Orion-Start
        // A paused track is being replaced, so wipe its bookkeeping.
        component.Active = false;
        component.CurrentPlaybackOffset = 0f;
        component.PlaybackStartTime = null;
        // Orion-End

        DirectSetVisualState(uid, JukeboxVisualState.Select);
        component.Selecting = true;
        // Arcane-Edit-End

        Dirty(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<JukeboxComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Selecting)
            {
                comp.SelectAccumulator += frameTime;
                if (comp.SelectAccumulator >= 0.5f)
                {
                    comp.SelectAccumulator = 0f;
                    comp.Selecting = false;

                    TryUpdateVisualState(uid, comp);
                }
            }


            // Arcane-Start
            if (comp.Active && comp.Playing && !comp.LoopEnabled && comp.PlaybackStartTime is { } start &&
                comp.SelectedSongId is { } songId && _protoManager.Resolve(songId, out var songProto) &&
                Audio.GetAudioLength(songProto.Path.Path.ToString()).TotalSeconds <=
                comp.CurrentPlaybackOffset + (float) (_gameTiming.CurTime - start).TotalSeconds)
            {
                Stop((uid, comp));
            }
            // Arcane-End
        }
    }

    // Orion-Start
    private void SetJukeboxVolume(EntityUid uid, JukeboxComponent component, float volume)
    {
        component.Volume = Math.Clamp(volume, component.MinSlider, component.MaxSlider); // Arcane-Edit
        Dirty(uid, component);
    }

    private void ToggleLoop(EntityUid uid, JukeboxComponent component)
    {
        component.LoopEnabled = !component.LoopEnabled;
        Dirty(uid, component);
    }
    // Orion-End

    // Arcane-Edit-Start
    // private void OnComponentShutdown(EntityUid uid, JukeboxComponent component, ComponentShutdown args)
    // {
    //     component.AudioStream = Audio.Stop(component.AudioStream);
    // }
    // Arcane-Edit-End

    private void DirectSetVisualState(EntityUid uid, JukeboxVisualState state)
    {
        _appearanceSystem.SetData(uid, JukeboxVisuals.VisualState, state);
    }

    private void TryUpdateVisualState(EntityUid uid, JukeboxComponent? jukeboxComponent = null)
    {
        if (!Resolve(uid, ref jukeboxComponent))
            return;

        var finalState = JukeboxVisualState.On;

        if (!this.IsPowered(uid, EntityManager))
        {
            finalState = JukeboxVisualState.Off;
        }

        _appearanceSystem.SetData(uid, JukeboxVisuals.VisualState, finalState);
    }
}
