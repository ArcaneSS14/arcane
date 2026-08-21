// SPDX-License-Identifier: MIT

using Content.Server.Chat.Systems;
using Content.Shared.Radio.Components;
using Content.Server._EinsteinEngines.Language;
using Content.Shared.Chat;
using Content.Shared.Inventory.Events;
using Content.Shared.Radio;
using Content.Shared.Radio.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Content.Shared.Whitelist;
// Arcane-Start
using Content.Shared._Arcane.TTS;
using Content.Goobstation.Common.Barks;
using Content.Shared._Orion.Radio;
using Robust.Shared.Audio;
// Arcane-End

namespace Content.Server.Radio.EntitySystems;

public sealed class HeadsetSystem : SharedHeadsetSystem
{
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!; // Goobstation

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HeadsetComponent, RadioReceiveEvent>(OnHeadsetReceive);
        SubscribeLocalEvent<HeadsetComponent, EncryptionChannelsChangedEvent>(OnKeysChanged);

        SubscribeLocalEvent<WearingHeadsetComponent, EntitySpokeEvent>(OnSpeak);
        SubscribeLocalEvent<HeadsetComponent, RadioReceiveAttemptEvent>(OnHeadsetReceiveAttempt); // Goobstation - Whitelisted radio channel
    }

    private void OnKeysChanged(EntityUid uid, HeadsetComponent component, EncryptionChannelsChangedEvent args)
    {
        UpdateRadioChannels(uid, component, args.Component);
    }

    private void UpdateRadioChannels(EntityUid uid, HeadsetComponent headset, EncryptionKeyHolderComponent? keyHolder = null)
    {
        // make sure to not add ActiveRadioComponent when headset is being deleted
        if (!headset.Enabled || MetaData(uid).EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        if (!Resolve(uid, ref keyHolder))
            return;

        if (keyHolder.Channels.Count == 0)
            RemComp<ActiveRadioComponent>(uid);
        else
            EnsureComp<ActiveRadioComponent>(uid).Channels = new(keyHolder.Channels);
    }

    private void OnSpeak(EntityUid uid, WearingHeadsetComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null
            && TryComp(component.Headset, out EncryptionKeyHolderComponent? keys)
            && keys.Channels.Contains(args.Channel.ID)
            && _whitelist.IsWhitelistPassOrNull(args.Channel.SendWhitelist, uid)) // Goobstation - Whitelisted channels
        {
            // Arcane-Edit-Start
            if (_radio.SendRadioMessage(uid, args.Message, args.Channel, component.Headset))
            {
                args.RadioMessageSent = true;
                args.Channel = null; // prevent duplicate messages from other listeners.
            }
            // Arcane-Edit-End
        }
    }

    protected override void OnGotEquipped(EntityUid uid, HeadsetComponent component, GotEquippedEvent args)
    {
        base.OnGotEquipped(uid, component, args);
        if (component.IsEquipped && component.Enabled)
        {
            EnsureComp<WearingHeadsetComponent>(args.Equipee).Headset = uid;
            UpdateRadioChannels(uid, component);
        }
    }

    protected override void OnGotUnequipped(EntityUid uid, HeadsetComponent component, GotUnequippedEvent args)
    {
        base.OnGotUnequipped(uid, component, args);
        RemComp<ActiveRadioComponent>(uid);
        RemComp<WearingHeadsetComponent>(args.Equipee);
    }

    public void SetEnabled(EntityUid uid, bool value, HeadsetComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.Enabled == value)
            return;

        component.Enabled = value;
        Dirty(uid, component);

        if (!value)
        {
            RemCompDeferred<ActiveRadioComponent>(uid);

            if (component.IsEquipped)
                RemCompDeferred<WearingHeadsetComponent>(Transform(uid).ParentUid);
        }
        else if (component.IsEquipped)
        {
            EnsureComp<WearingHeadsetComponent>(Transform(uid).ParentUid).Headset = uid;
            UpdateRadioChannels(uid, component);
        }
    }

    private static readonly SoundSpecifier DefaultOnSound = new SoundPathSpecifier("/Audio/_Orion/Radio/basic.ogg"); // Arcane

    private void OnHeadsetReceive(EntityUid uid, HeadsetComponent component, ref RadioReceiveEvent args)
    {
        // TODO: change this when a code refactor is done
        // this is currently done this way because receiving radio messages on an entity otherwise requires that entity
        // to have an ActiveRadioComponent

        // Einstein Engines - Language begin
        var parent = Transform(uid).ParentUid;

        if (parent.IsValid())
        {
            var relayEvent = new HeadsetRadioReceiveRelayEvent(args);
            RaiseLocalEvent(parent, ref relayEvent);
        }

        if (TryComp(parent, out ActorComponent? actor))
        {
            var canUnderstand = _language.CanUnderstand(parent, args.Language.ID);
            var msg = new MsgChatMessage
            {
                Message = canUnderstand ? args.OriginalChatMsg : args.LanguageObfuscatedChatMsg
            };

            // Arcane-Start
            if (canUnderstand && args.Voice is { } voice)
            {
                var ev = new TTSRadioPlayEvent(args.OriginalChatMsg.Message, args.Language, voice);
                RaiseLocalEvent(parent, ref ev);
            }
            // Arcane-End

            _netMan.ServerSendMessage(msg, actor.PlayerSession.Channel);

            // Arcane-Start: Radio sound
            var sound = args.Channel.OnSendSound ?? DefaultOnSound;
            if (sound is SoundPathSpecifier sps)
            {
                RaiseNetworkEvent(new PlayRadioBarkEvent
                {
                    Path = sps.Path.ToString(),
                    Params = sps.Params,
                    Source = GetNetEntity(args.MessageSource),
                }, actor.PlayerSession.Channel);
            }
            else if (sound is SoundCollectionSpecifier)
            {
                Log.Warning($"Radio channel {args.Channel.ID} uses SoundCollectionSpecifier, which is not supported for PlayRadioBarkEvent. Falling back to silent playback.");
            }

            if (parent != args.MessageSource
                && TryComp<SpeechSynthesisComponent>(args.MessageSource, out var speech)
                && speech.VoicePrototypeId is { } barkVoice)
            {
                RaiseNetworkEvent(
                    new PlayBarkEvent(GetNetEntity(args.MessageSource), args.OriginalChatMsg.Message, false, barkVoice),
                    actor.PlayerSession.Channel);
            }
            // Arcane-End
        }
        // Einstein Engines - Language end
    }

    // Goobstation - Whitelisted radio channel
    private void OnHeadsetReceiveAttempt(EntityUid uid, HeadsetComponent component, ref RadioReceiveAttemptEvent args)
    {
        args.Cancelled |= _whitelist.IsWhitelistFail(args.Channel.ReceiveWhitelist, uid);
    }
}
