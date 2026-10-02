using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Systems;
using Content.Shared._Arcane.CCVars;
using Content.Shared.GameTicking;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Client._Arcane.StationRadio;

public sealed class StationRadioReceiverAudioSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;

    private readonly Dictionary<EntityUid, (EntityUid Stream, int PlaybackId)> _localStreams = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationRadioReceiverComponent, AfterAutoHandleStateEvent>(OnReceiverAfterState);
        SubscribeLocalEvent<StationRadioReceiverComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<StationRadioReceiverComponent, ComponentShutdown>(OnReceiverShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        _cfg.OnValueChanged(ACCVars.StationRadioVolume, OnStationRadioVolumeChanged);
    }

    public override void Shutdown()
    {
        _cfg.UnsubValueChanged(ACCVars.StationRadioVolume, OnStationRadioVolumeChanged);
        StopAllLocalStreams();
        base.Shutdown();
    }

    private void OnReceiverAfterState(Entity<StationRadioReceiverComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplyReceiverState(ent);
    }

    private void OnPowerChanged(EntityUid uid, StationRadioReceiverComponent component, PowerChangedEvent args)
    {
        ApplyVolume(uid, component);
    }

    private void OnReceiverShutdown(EntityUid uid, StationRadioReceiverComponent component, ComponentShutdown args)
    {
        StopLocalStream(uid);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        StopAllLocalStreams();
    }

    private void OnStationRadioVolumeChanged(float _)
    {
        foreach (var (receiver, _) in _localStreams)
        {
            if (TryComp<StationRadioReceiverComponent>(receiver, out var component))
                ApplyVolume(receiver, component);
        }
    }

    private void ApplyReceiverState(Entity<StationRadioReceiverComponent> ent)
    {
        var (uid, component) = ent;
        if (component.CurrentMedia == null)
        {
            StopLocalStream(uid);
            return;
        }

        if (_localStreams.TryGetValue(uid, out var local)
            && (!Exists(local.Stream) || local.PlaybackId != component.PlaybackId))
        {
            StopLocalStream(uid);
        }

        if (!_localStreams.ContainsKey(uid))
            TryCreateLocalStream(uid, component);

        ApplyVolume(uid, component);
    }

    private void TryCreateLocalStream(EntityUid uid, StationRadioReceiverComponent component)
    {
        var media = component.CurrentMedia;
        if (media == null)
            return;

        var path = _audio.GetAudioPath(media);
        if (string.IsNullOrEmpty(path))
            return;

        var resource = _resourceCache.GetResource<AudioResource>(path);
        var audioParams = component.DefaultParams.WithVolume(float.NegativeInfinity);
        if (component.MediaStartTime is { } startTime)
        {
            var offset = Math.Max((float) (_timing.CurTime - startTime).TotalSeconds, 0f);
            audioParams = audioParams.WithPlayOffset(offset);
        }

        var stream = _audio.PlayEntity(resource.AudioStream, uid, media, audioParams);
        if (stream == null)
            return;

        _localStreams[uid] = (stream.Value.Entity, component.PlaybackId);
    }

    private void ApplyVolume(EntityUid uid, StationRadioReceiverComponent component)
    {
        if (!_localStreams.TryGetValue(uid, out var local) || !Exists(local.Stream))
            return;

        var volume = StationRadioReceiverSystem.ComputeVolumeForRadio(
            component.DefaultParams.Volume,
            _cfg.GetCVar(ACCVars.StationRadioVolume),
            _power.IsPowered(uid),
            component.Active);

        _audio.SetVolume(local.Stream, volume);
    }

    private void StopLocalStream(EntityUid uid)
    {
        if (!_localStreams.Remove(uid, out var local))
            return;

        if (Exists(local.Stream))
            _audio.Stop(local.Stream);
    }

    private void StopAllLocalStreams()
    {
        foreach (var (_, local) in _localStreams)
        {
            if (Exists(local.Stream))
                _audio.Stop(local.Stream);
        }

        _localStreams.Clear();
    }
}
