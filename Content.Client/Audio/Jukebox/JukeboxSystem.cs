// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Audio.Jukebox;
using Content.Shared.GameTicking;
using Content.Shared._Arcane.CCVars;
using Robust.Client.Animations;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Audio.Jukebox;


public sealed class JukeboxSystem : SharedJukeboxSystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly AnimationPlayerSystem _animationPlayer = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    // Arcane-Start
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float PlaybackRange = 10f;
    private const float PositionSyncTolerance = 0.5f;
    private readonly Dictionary<EntityUid, EntityUid> _localStreams = new();
    // Arcane-End

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JukeboxComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<JukeboxComponent, AnimationCompletedEvent>(OnAnimationCompleted);
        SubscribeLocalEvent<JukeboxComponent, AfterAutoHandleStateEvent>(OnJukeboxAfterState);
        // Arcane-Start
        SubscribeLocalEvent<JukeboxComponent, ComponentShutdown>(OnJukeboxShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        // Arcane-End

        _protoManager.PrototypesReloaded += OnProtoReload;

        // Arcane-Start
        _cfg.OnValueChanged(ACCVars.JukeboxVolume, _ => ApplyPersonalVolume());
    }

    public EntityUid? GetLocalStream(EntityUid jukebox)
    {
        return _localStreams.GetValueOrDefault(jukebox);
    }

    public void ApplyLocalVolume(EntityUid jukebox, float volume)
    {
        if (!_localStreams.TryGetValue(jukebox, out var stream) || !Exists(stream))
            return;

        if (!TryComp<JukeboxComponent>(jukebox, out var comp))
            return;

        Audio.SetVolume(stream, GetEffectiveVolume(comp, volume));
    }
    // Arcane-End

    public override void Shutdown()
    {
        base.Shutdown();

        StopAllLocalStreams(); // Arcane
        _protoManager.PrototypesReloaded -= OnProtoReload;
    }

    // Arcane-Edit-Start
    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        StopAllLocalStreams();
    }

    private void StopAllLocalStreams()
    {
        foreach (var stream in _localStreams.Values)
        {
            if (Exists(stream))
                Audio.Stop(stream);
        }

        _localStreams.Clear();
    }

    private void OnJukeboxShutdown(EntityUid uid, JukeboxComponent component, ComponentShutdown args)
    {
        // Fires when the jukebox is deleted or leaves this client's PVS.
        StopLocalStream(uid);
    }

    private void ApplyPersonalVolume()
    {
        foreach (var (jukebox, stream) in _localStreams)
        {
            if (!TryComp<JukeboxComponent>(jukebox, out var comp))
                continue;

            Audio.SetVolume(stream, GetEffectiveVolume(comp));
        }
    }

    private void StopLocalStream(EntityUid jukebox)
    {
        if (!_localStreams.Remove(jukebox, out var stream))
            return;

        if (Exists(stream))
            Audio.Stop(stream);
    }

    private void OnJukeboxAfterState(Entity<JukeboxComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        // Arcane-Edit-Start
        ApplyJukeboxState(ent);
        // Arcane-Edit-End

        if (!_uiSystem.TryGetOpenUi<JukeboxBoundUserInterface>(ent.Owner, JukeboxUiKey.Key, out var bui))
            return;

        bui.Reload();
    }

    private void ApplyJukeboxState(Entity<JukeboxComponent> ent)
    {
        var (uid, comp) = ent;

        if (!comp.Active)
        {
            StopLocalStream(uid);
            return;
        }

        if (!_localStreams.TryGetValue(uid, out var stream) || !Exists(stream))
        {
            TryCreateLocalStream(uid, comp);
            return;
        }

        if (!TryComp<AudioComponent>(stream, out var audioComp))
            return;

        // Shared (server-authoritative) volume + the listener's personal jukebox.volume multiplier.
        Audio.SetVolume(stream, GetEffectiveVolume(comp));

        // Looping is baked into the OpenAL source, so a loop toggle needs a fresh source.
        if (audioComp.Params.Loop != comp.LoopEnabled)
        {
            StopLocalStream(uid);
            TryCreateLocalStream(uid, comp);
            return;
        }

        if (audioComp.Playing != comp.Playing)
            Audio.SetState(stream, comp.Playing ? AudioState.Playing : AudioState.Paused);

        SyncPosition(stream, audioComp, comp);
    }

    private void TryCreateLocalStream(EntityUid uid, JukeboxComponent comp)
    {
        if (string.IsNullOrEmpty(comp.SelectedSongId) ||
            !_protoManager.Resolve(comp.SelectedSongId, out var songProto))
            return;

        var audioResource = _resourceCache.GetResource<AudioResource>(songProto.Path.Path.ToString());

        var par = AudioParams.Default
            .WithVolume(GetEffectiveVolume(comp))
            .WithMaxDistance(PlaybackRange)
            .WithLoop(comp.LoopEnabled);

        // The stream buffer is already loaded and passed explicitly; the specifier only wires up
        // AudioComponent.FileName, which SetPlaybackPosition needs for its length lookup.
        var stream = _audio.PlayEntity(audioResource.AudioStream, uid, songProto.Path.Path, par)?.Entity;
        if (stream == null)
            return;

        _localStreams[uid] = stream.Value;

        if (!comp.Playing)
        // Arcane-Edit-Start
        {
            Audio.SetVolume(stream.Value, float.NegativeInfinity);
            Audio.SetState(stream.Value, AudioState.Paused);
            Audio.SetVolume(stream.Value, GetEffectiveVolume(comp));
        }
        // Arcane-Edit-End

        var (_, target) = GetPlaybackPosition(comp);
        if (target > 0.1f)
            Audio.SetPlaybackPosition(stream.Value, target);
    }

    private void SyncPosition(EntityUid stream, AudioComponent audioComp, JukeboxComponent comp)
    {
        var (length, target) = GetPlaybackPosition(comp);
        var diff = length > 0f
            ? MathF.Abs(MathF.IEEERemainder(target - audioComp.PlaybackPosition, length))
            : MathF.Abs(target - audioComp.PlaybackPosition);

        // The server timeline is linear while our looping source's OpenAL offset keeps growing, and a
        // listener may have joined at any point inside a loop. Only re-anchor when the sources are
        // genuinely out of sync modulo the track length, so normal looping never triggers a seek.
        if (diff > PositionSyncTolerance)
            Audio.SetPlaybackPosition(stream, target);
    }

    private float GetEffectiveVolume(JukeboxComponent comp, float? volumeOverride = null) // Arcane-Edit
    {
        var multiplier = _cfg.GetCVar(ACCVars.JukeboxVolume);
        var personal = multiplier <= 0.01f
            ? float.NegativeInfinity
            : SharedAudioSystem.GainToVolume(multiplier);

        // Arcane-Edit-Start
        var shared = volumeOverride is { } volume
            ? SharedJukeboxSystem.MapToRange(volume, comp.MinSlider, comp.MaxSlider, comp.MinVolume, comp.MaxVolume)
            : SharedJukeboxSystem.GetAudioVolume(comp);

        return shared + personal;
        // Arcane-Edit-End
    }

    /// <summary>
    /// Server-authoritative playback position wrapped into the track length so it stays seekable
    /// on the looping client source (the shared SetPlaybackPosition deletes entities past the end).
    /// The length is 0 when no song is resolved; callers use it to pick the wrap mode.
    /// </summary>
    private (float Length, float Position) GetPlaybackPosition(JukeboxComponent comp)
    {
        var pos = comp.CurrentPlaybackOffset;
        if (comp.Playing && comp.PlaybackStartTime is { } start)
            pos += (float) (_timing.CurTime - start).TotalSeconds;

        pos = MathF.Max(pos, 0f);

        var length = 0f;
        if (comp.SelectedSongId is { } songId && _protoManager.Resolve(songId, out var songProto))
            length = MathF.Max((float) Audio.GetAudioLength(new ResolvedPathSpecifier(songProto.Path.Path)).TotalSeconds, 0.01f);

        if (length > 0f)
        {
            pos %= length;
            pos = MathF.Min(pos, length - 0.01f);
        }

        return (length, pos);
    }
    // Arcane-Edit-End

    private void OnProtoReload(PrototypesReloadedEventArgs obj)
    {
        if (!obj.WasModified<JukeboxPrototype>())
            return;

        var query = AllEntityQuery<JukeboxComponent, UserInterfaceComponent>();

        while (query.MoveNext(out var uid, out _, out var ui))
        {
            if (!_uiSystem.TryGetOpenUi<JukeboxBoundUserInterface>((uid, ui), JukeboxUiKey.Key, out var bui))
                continue;

            bui.PopulateMusic();
        }
    }

    // Arcane-Edit-Start
    // private void OnJukeboxAfterState(Entity<JukeboxComponent> ent, ref AfterAutoHandleStateEvent args)
    // {
    //     if (!_uiSystem.TryGetOpenUi<JukeboxBoundUserInterface>(ent.Owner, JukeboxUiKey.Key, out var bui))
    //         return;

    //     bui.Reload();
    // }
    // Arcane-Edit-End

    private void OnAnimationCompleted(EntityUid uid, JukeboxComponent component, AnimationCompletedEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!TryComp<AppearanceComponent>(uid, out var appearance) ||
            !_appearanceSystem.TryGetData<JukeboxVisualState>(uid, JukeboxVisuals.VisualState, out var visualState, appearance))
        {
            visualState = JukeboxVisualState.On;
        }

        UpdateAppearance((uid, sprite), visualState, component);
    }

    private void OnAppearanceChange(EntityUid uid, JukeboxComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.AppearanceData.TryGetValue(JukeboxVisuals.VisualState, out var visualStateObject) ||
            visualStateObject is not JukeboxVisualState visualState)
        {
            visualState = JukeboxVisualState.On;
        }

        UpdateAppearance((uid, args.Sprite), visualState, component);
    }

    private void UpdateAppearance(Entity<SpriteComponent> entity, JukeboxVisualState visualState, JukeboxComponent component)
    {
        SetLayerState(JukeboxVisualLayers.Base, component.OffState, entity);

        switch (visualState)
        {
            case JukeboxVisualState.On:
                SetLayerState(JukeboxVisualLayers.Base, component.OnState, entity);
                break;

            case JukeboxVisualState.Off:
                SetLayerState(JukeboxVisualLayers.Base, component.OffState, entity);
                break;

            case JukeboxVisualState.Select:
                PlayAnimation(entity.Owner, JukeboxVisualLayers.Base, component.SelectState, 1.0f, entity);
                break;
        }
    }

    private void PlayAnimation(EntityUid uid, JukeboxVisualLayers layer, string? state, float animationTime, SpriteComponent sprite)
    {
        if (string.IsNullOrEmpty(state))
            return;

        if (!_animationPlayer.HasRunningAnimation(uid, state))
        {
            var animation = GetAnimation(layer, state, animationTime);
            _sprite.LayerSetVisible((uid, sprite), layer, true);
            _animationPlayer.Play(uid, animation, state);
        }
    }

    private static Animation GetAnimation(JukeboxVisualLayers layer, string state, float animationTime)
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(animationTime),
            AnimationTracks =
                {
                    new AnimationTrackSpriteFlick
                    {
                        LayerKey = layer,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(state, 0f)
                        }
                    }
                }
        };
    }

    private void SetLayerState(JukeboxVisualLayers layer, string? state, Entity<SpriteComponent> sprite)
    {
        if (string.IsNullOrEmpty(state))
            return;

        _sprite.LayerSetVisible(sprite.AsNullable(), layer, true);
        _sprite.LayerSetAutoAnimated(sprite.AsNullable(), layer, true);
        _sprite.LayerSetRsiState(sprite.AsNullable(), layer, state);
    }
}
