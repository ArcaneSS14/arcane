// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;  // goob - intermap transmitters
using Content.Goobstation.Shared.Communications; // goob - intermap transmitters
using Content.Goobstation.Shared.Loudspeaker.Events; // goob - loudspeakers
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server._EinsteinEngines.Language;
using Content.Server.Power.Components;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared._EinsteinEngines.Language;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Speech;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Replays;
using Robust.Shared.Utility;
using Content.Shared.Access.Systems; // Goobstation
using Content.Shared.Chat.RadioIconsEvents; // Goobstation
using Content.Shared.Whitelist; // Goobstation
using Content.Shared.StatusIcon; // Goobstation
using Content.Goobstation.Shared.Radio; // Goobstation
// Arcane-Start
using Content.Shared._Arcane.TTS; // Arcane
using Content.Goobstation.Common.Barks;
using Content.Shared._Orion.Radio;
using Robust.Shared.Audio;
// Arcane-End

namespace Content.Server.Radio.EntitySystems;

/// <summary>
///     This system handles intrinsic radios and the general process of converting radio messages into chat messages.
/// </summary>
public sealed partial class RadioSystem : EntitySystem
{
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly IReplayRecordingManager _replay = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly RadioJobIconSystem _radioIconSystem = default!; // Goobstation - radio icons
    [Dependency] private readonly LanguageSystem _language = default!; // Einstein Engines - Language
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!; // Goobstation - Whitelisted radio channels

    // set used to prevent radio feedback loops.
    private readonly HashSet<string> _messages = new();

    private EntityQuery<TelecomExemptComponent> _exemptQuery;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, RadioReceiveEvent>(OnIntrinsicReceive);
        SubscribeLocalEvent<IntrinsicRadioTransmitterComponent, EntitySpokeEvent>(OnIntrinsicSpeak);
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, RadioReceiveAttemptEvent>(OnIntrinsicReceiveAttempt); // Goobstation

        _exemptQuery = GetEntityQuery<TelecomExemptComponent>();
    }

    private void OnIntrinsicSpeak(EntityUid uid, IntrinsicRadioTransmitterComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null
            && component.Channels.Contains(args.Channel.ID)
            && _whitelist.IsWhitelistPassOrNull(args.Channel.SendWhitelist, uid)) // Goobstation - Whitelisted radio channels
        {
            // Arcane-Edit-Start
            if (SendRadioMessage(uid, args.Message, args.Channel, uid, args.Language)) // Einstein Engines - Language
            {
                args.RadioMessageSent = true;
                args.Channel = null; // prevent duplicate messages from other listeners.
            }
            // Arcane-Edit-End
        }
    }

    private void OnIntrinsicReceive(EntityUid uid, IntrinsicRadioReceiverComponent component, ref RadioReceiveEvent args)
    {
        if (TryComp(uid, out ActorComponent? actor))
        {
            // Einstein Engines - Languages begin
            var listener = component.Owner;
            var msg = args.OriginalChatMsg;
            var canUnderstand = listener == null || _language.CanUnderstand(listener, args.Language.ID); // Arcane

            if (!canUnderstand)
                msg = args.LanguageObfuscatedChatMsg;

            // Arcane-Start
            if (canUnderstand && args.Voice is { } voice)
            {
                var ev = new TTSRadioPlayEvent(args.OriginalChatMsg.Message, args.Language, voice);
                RaiseLocalEvent(uid, ref ev);
            }
            // Arcane-End
            _netMan.ServerSendMessage(new MsgChatMessage { Message = msg }, actor.PlayerSession.Channel);

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

            if (uid != args.MessageSource
                && TryComp<SpeechSynthesisComponent>(args.MessageSource, out var speech)
                && speech.VoicePrototypeId is { } barkVoice)
            {
                RaiseNetworkEvent(
                    new PlayBarkEvent(GetNetEntity(args.MessageSource), args.OriginalChatMsg.Message, false, barkVoice),
                    actor.PlayerSession.Channel);
            }
            // Arcane-End
            // Einstein Engines - Languages end
        }
    }

    // Goobstation - Whitelisted radio channels
    private void OnIntrinsicReceiveAttempt(EntityUid uid, IntrinsicRadioReceiverComponent component, ref RadioReceiveAttemptEvent args)
    {
        args.Cancelled = _whitelist.IsWhitelistFail(args.Channel.ReceiveWhitelist, uid);
    }

    private static readonly SoundSpecifier DefaultOnSound = new SoundPathSpecifier("/Audio/_Orion/Radio/basic.ogg"); // Arcane

    /// <summary>
    /// Send radio message to all active radio listeners
    /// </summary>
    public bool SendRadioMessage( // Arcane-Edit
        EntityUid messageSource,
        string message,
        ProtoId<RadioChannelPrototype> channel,
        EntityUid radioSource,
        LanguagePrototype? language = null,
        bool escapeMarkup = true)
    {
        return SendRadioMessage(messageSource, message, _prototype.Index(channel), radioSource, escapeMarkup: escapeMarkup, language: language); // Einstein Engines - Language // Arcane-Edit
    }

    /// <summary>
    /// Send radio message to all active radio listeners
    /// </summary>
    /// <param name="messageSource">Entity that spoke the message</param>
    /// <param name="radioSource">Entity that picked up the message and will send it, e.g. headset</param>
    /// <returns>Whether the message was transmitted to at least one radio listener. // Arcane-Edit </returns>
    public bool SendRadioMessage( // Arcane-Edit
        EntityUid messageSource,
        string message,
        RadioChannelPrototype channel,
        EntityUid radioSource,
        LanguagePrototype? language = null,
        bool escapeMarkup = true)
    {
        // Einstein Engines - Language begin
        if (language == null)
            language = _language.GetLanguage(messageSource);

        if (!language.SpeechOverride.AllowRadio)
            return false; // Arcane-Edit
        // Einstein Engines - Language end

        // TODO if radios ever garble / modify messages, feedback-prevention needs to be handled better than this.
        if (!_messages.Add(message))
            return false; // Arcane-Edit

        var evt = new TransformSpeakerNameEvent(messageSource, MetaData(messageSource).EntityName);
        RaiseLocalEvent(messageSource, evt);

        // Goob - Job icons
        if (_radioIconSystem.TryGetJobIcon(messageSource, out var jobIcon, out var jobName))
        {
            var iconEvent = new TransformSpeakerJobIconEvent(messageSource, jobIcon.Value, jobName);
            RaiseLocalEvent(messageSource, iconEvent);

            jobIcon = iconEvent.JobIcon;
            jobName = iconEvent.JobName;
        }

        var name = evt.VoiceName;
        name = FormattedMessage.EscapeText(name);

        SpeechVerbPrototype speech;
        if (evt.SpeechVerb != null && _prototype.Resolve(evt.SpeechVerb, out var evntProto))
            speech = evntProto;
        else
            speech = _chat.GetSpeechVerb(messageSource, message);

        var content = escapeMarkup
            ? FormattedMessage.EscapeText(message)
            : message;

        // var wrappedMessage = Loc.GetString(speech.Bold ? "chat-radio-message-wrap-bold" : "chat-radio-message-wrap",
        //     ("color", channel.Color),
        //     ("fontType", speech.FontId),
        //     ("fontSize", speech.FontSize),
        //     ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
        //     ("channel", $"\\[{channel.LocalizedName}\\]"),
        //     ("name", name),
        //     ("message", content));
        var wrappedMessage = WrapRadioMessage(messageSource, channel, name, content, language, jobIcon, jobName); // Einstein Engines - Language

        // most radios are relayed to chat, so lets parse the chat message beforehand
        // var chat = new ChatMessage(
        //     ChatChannel.Radio,
        //     message,
        //     wrappedMessage,
        //     NetEntity.Invalid,
        //     null);
        // var chatMsg = new MsgChatMessage { Message = chat };
        // var ev = new RadioReceiveEvent(message, messageSource, channel, radioSource, chatMsg);
        // Goobstation - Chat Pings
        // Added GetNetEntity(messageSource), to source
        var msg = new ChatMessage(ChatChannel.Radio, content, wrappedMessage, GetNetEntity(messageSource), null);

        // Einstein Engines - Language begin
        var obfuscated = _language.ObfuscateSpeech(content, language);
        // Goobstation - Chat Pings
        // Added GetNetEntity(messageSource), to source
        var obfuscatedWrapped = WrapRadioMessage(messageSource, channel, name, obfuscated, language, jobIcon, jobName);
        var notUdsMsg = new ChatMessage(ChatChannel.Radio, obfuscated, obfuscatedWrapped, GetNetEntity(messageSource), null);

        // Arcane-Start
        string? voice = null;
        if (TryComp<TTSComponent>(messageSource, out var ttsComponent)
            && ttsComponent.VoicePrototype is { } voiceId
            && _prototype.TryIndex(voiceId, out var voicePrototype))
            voice = voicePrototype.Speaker;
        // Arcane-End
        var ev = new RadioReceiveEvent(messageSource, channel, msg, notUdsMsg, language, radioSource, voice); // Arcane-Edit
        // Einstein Engines - Language end

        var sendAttemptEv = new RadioSendAttemptEvent(channel, radioSource);
        RaiseLocalEvent(ref sendAttemptEv);
        RaiseLocalEvent(radioSource, ref sendAttemptEv);
        var canSend = !sendAttemptEv.Cancelled;

        var sourceMapId = Transform(radioSource).MapID;
        var hasActiveServer = HasActiveServer(sourceMapId, channel.ID);
        var sourceServerExempt = _exemptQuery.HasComp(radioSource);

        var radioQuery = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        var sent = false; // Arcane
        while (canSend && radioQuery.MoveNext(out var receiver, out var radio, out var transform))
        {
            if (!radio.ReceiveAllChannels)
            {
                if (!radio.Channels.Contains(channel.ID) || (TryComp<IntercomComponent>(receiver, out var intercom) &&
                                                             !intercom.SupportedChannels.Contains(channel.ID)))
                    continue;
            }

            if (!channel.LongRange && transform.MapID != sourceMapId && !radio.GlobalReceive
                && !(HasActiveTransmitter(transform.MapID) && HasActiveTransmitter(sourceMapId))) // goob - intermap transmitters
                continue;

            // don't need telecom server for long range channels or handheld radios and intercoms
            var needServer = !channel.LongRange && !sourceServerExempt;
            if (needServer && !hasActiveServer)
                continue;

            // check if message can be sent to specific receiver
            var attemptEv = new RadioReceiveAttemptEvent(channel, radioSource, receiver);
            RaiseLocalEvent(ref attemptEv);
            RaiseLocalEvent(receiver, ref attemptEv);
            if (attemptEv.Cancelled)
                continue;

            // send the message
            RaiseLocalEvent(receiver, ref ev);
            sent = true; // Arcane
        }

        if (name != Name(messageSource))
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} as {name} on {channel.LocalizedName}: {message}");
        else
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} on {channel.LocalizedName}: {message}");

        _replay.RecordServerMessage(msg); // Einstein Engines - Language
        _messages.Remove(message);
        return sent; // Arcane
    }

    // Einstein Engines - Language begin
    private string WrapRadioMessage(
        EntityUid source,
        RadioChannelPrototype channel,
        string name,
        string message,
        LanguagePrototype language,
        ProtoId<JobIconPrototype>? jobIcon, // Goob edit
        string? jobName = null) // Gaby Radio icons
    {
        // TODO: code duplication with ChatSystem.WrapMessage
        var speech = _chat.GetSpeechVerb(source, message);
        var languageColor = channel.Color;

        // Goobstation - Bolded Language Overrides begin
        var wrapId = speech.Bold ? "chat-radio-message-wrap-bold" : "chat-radio-message-wrap";
        if (speech.Bold && language.SpeechOverride.BoldFontId != null)
            wrapId = "chat-radio-message-wrap-bolded-language";
        // Goobstation end

        if (language.SpeechOverride.Color is { } colorOverride)
            languageColor = Color.InterpolateBetween(Color.White, colorOverride, colorOverride.A); // Changed first param to Color.White so it shows color correctly.

        var languageDisplay = language.IsVisibleLanguage
            ? Loc.GetString("chat-manager-language-prefix", ("language", language.ChatName))
            : "";

        // goob start - loudspeakers

        int? loudSpeakFont = null;

        var getLoudspeakerEv = new GetLoudspeakerEvent();
        RaiseLocalEvent(source, ref getLoudspeakerEv);

        if (getLoudspeakerEv.Loudspeakers != null)
            foreach (var loudspeaker in getLoudspeakerEv.Loudspeakers)
            {
                var loudSpeakerEv = new GetLoudspeakerDataEvent();
                RaiseLocalEvent(loudspeaker, ref loudSpeakerEv);

                if (loudSpeakerEv.IsActive && loudSpeakerEv.AffectRadio)
                {
                    loudSpeakFont = loudSpeakerEv.FontSize;
                    break;
                }
            }

        var nameString = jobIcon is null // (unrelated to loudspeakers but still goob)
            ? name
            : Loc.GetString("chat-radio-message-name-with-icon", ("jobIcon", jobIcon), ("jobName", jobName ?? ""), ("name", name));
        // goob end

        return Loc.GetString(wrapId,
            ("color", channel.Color),
            ("languageColor", languageColor),
            ("fontType", language.SpeechOverride.FontId ?? speech.FontId),
            ("fontSize", loudSpeakFont ?? language.SpeechOverride.FontSize ?? speech.FontSize), // goob edit - "loudSpeakFont"
            ("boldFontType", language.SpeechOverride.BoldFontId ?? language.SpeechOverride.FontId ?? speech.FontId), // Goob Edit - Custom Bold Fonts
            ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
            ("channel", $"\\[{channel.LocalizedName}\\]"),
            ("name", nameString), // goob
            ("message", message),
            ("language", languageDisplay));
    }
    // Einstein Engines - Language end

    /// <inheritdoc cref="TelecomServerComponent"/>
    private bool HasActiveServer(MapId mapId, string channelId)
    {
        var servers = EntityQuery<TelecomServerComponent, EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>();
        foreach (var (_, keys, power, transform) in servers)
        {
            if (transform.MapID == mapId &&
                power.Powered &&
                keys.Channels.Contains(channelId))
            {
                return true;
            }
        }
        return false;
    }

    /// <inheritdoc cref="TelecomServerComponent"/>
    private bool HasActiveTransmitter(MapId mapId)
    {
        return EntityQuery<TelecomTransmitterComponent, ApcPowerReceiverComponent, TransformComponent>()
            .Any(server => server.Item3.MapID == mapId && server.Item2.Powered);
    }
    // goob end
}
