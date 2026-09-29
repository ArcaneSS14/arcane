using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Verbs;
using Content.Shared._Arcane.Radio;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Server.GameObjects;
using Robust.Server.Player;

namespace Content.Server._Arcane.Radio;

/// <summary>
///     Lets a player mute receiving radio messages per channel through a BUI on a headset
///     or through an action on an intrinsic radio (IPC, silicons, borgs).
///     The muted set is per player session, so only delivery to that player is skipped;
///     the player can still broadcast on the same frequencies.
/// </summary>
public sealed class HeadsetChannelMuteSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    private static readonly EntProtoId OpenRadioChannelsAction = "ActionOpenRadioChannels";

    private readonly Dictionary<NetUserId, HashSet<int>> _mutedFrequencies = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<HeadsetComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<HeadsetComponent, HeadsetChannelMuteMessage>(OnToggleMute);
        SubscribeLocalEvent<HeadsetComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, MapInitEvent>(OnRadioReceiverMapInit);
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, OpenRadioChannelsActionEvent>(OnOpenRadioChannels);
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, BoundUIOpenedEvent>(OnIntrinsicUiOpened);
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, HeadsetChannelMuteMessage>(OnIntrinsicToggleMute);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
        _mutedFrequencies.Clear();
        base.Shutdown();
    }

    public bool IsMuted(NetUserId userId, int frequency)
    {
        return _mutedFrequencies.TryGetValue(userId, out var muted) && muted.Contains(frequency);
    }

    private void OnGetVerbs(EntityUid uid, HeadsetComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        _ui.SetUi(uid, HeadsetChannelUiKey.Key,
            new InterfaceData("HeadsetChannelBoundUserInterface", interactionRange: 0, requireInputValidation: false));

        var verb = new Verb
        {
            Priority = 1,
            Text = Loc.GetString("headset-channels-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Impact = LogImpact.Low,
            DoContactInteraction = true,
            Act = () => _ui.TryOpenUi(uid, HeadsetChannelUiKey.Key, args.User),
        };
        args.Verbs.Add(verb);
    }

    private void OnUiOpened(Entity<HeadsetComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is not HeadsetChannelUiKey.Key)
            return;

        UpdateUiState(ent.Owner, args.Actor);
    }

    private void OnRadioReceiverMapInit(Entity<IntrinsicRadioReceiverComponent> ent, ref MapInitEvent args)
    {
        EntityUid? actionId = null;
        _actions.AddAction(ent.Owner, ref actionId, OpenRadioChannelsAction);
    }

    private void OnOpenRadioChannels(Entity<IntrinsicRadioReceiverComponent> ent, ref OpenRadioChannelsActionEvent args)
    {
        if (!TryComp<ActorComponent>(args.Performer, out var actor))
            return;

        _ui.SetUi(ent.Owner, HeadsetChannelUiKey.Key,
            new InterfaceData("HeadsetChannelBoundUserInterface", interactionRange: 0, requireInputValidation: false));
        _ui.TryOpenUi(ent.Owner, HeadsetChannelUiKey.Key, actor.Owner);
    }

    private void OnIntrinsicUiOpened(Entity<IntrinsicRadioReceiverComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is not HeadsetChannelUiKey.Key)
            return;

        UpdateUiState(ent.Owner, args.Actor);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected)
            return;

        _mutedFrequencies.Remove(e.Session.UserId);
    }

    private void OnToggleMute(Entity<HeadsetComponent> ent, ref HeadsetChannelMuteMessage args)
    {
        if (!TryComp<ActorComponent>(args.Actor, out var actor))
            return;

        HandleToggleMute(ent.Owner, actor, args);
    }

    private void OnIntrinsicToggleMute(Entity<IntrinsicRadioReceiverComponent> ent, ref HeadsetChannelMuteMessage args)
    {
        if (!TryComp<ActorComponent>(args.Actor, out var actor))
            return;

        HandleToggleMute(ent.Owner, actor, args);
    }

    private void HandleToggleMute(EntityUid uid, ActorComponent actor, HeadsetChannelMuteMessage args)
    {
        if (args.Frequency <= 0)
            return;

        var channels = GetChannels(uid);
        if (!IsChannelOnFrequency(channels, args.Frequency))
            return;

        var userId = actor.PlayerSession.UserId;
        if (!_mutedFrequencies.TryGetValue(userId, out var muted))
            _mutedFrequencies[userId] = muted = new HashSet<int>();

        if (args.Muted)
        {
            if (muted.Count < _prototypes.Count<RadioChannelPrototype>())
                muted.Add(args.Frequency);
        }
        else
            muted.Remove(args.Frequency);

        UpdateUiState(uid, args.Actor);
    }

    private HashSet<int> GetMuted(EntityUid actor)
    {
        if (TryComp<ActorComponent>(actor, out var actorComp)
            && _mutedFrequencies.TryGetValue(actorComp.PlayerSession.UserId, out var muted))
            return muted;

        return new();
    }

    private List<ProtoId<RadioChannelPrototype>> GetChannels(EntityUid uid)
    {
        var channels = new HashSet<ProtoId<RadioChannelPrototype>>();

        if (TryComp<EncryptionKeyHolderComponent>(uid, out var keys))
            channels.UnionWith(keys.Channels);
        if (TryComp<ActiveRadioComponent>(uid, out var active))
            channels.UnionWith(active.Channels);
        if (TryComp<IntrinsicRadioTransmitterComponent>(uid, out var transmitter))
            channels.UnionWith(transmitter.Channels);

        return channels.ToList();
    }

    private bool IsChannelOnFrequency(List<ProtoId<RadioChannelPrototype>> channels, int frequency)
    {
        return channels.Any(channel =>
            _prototypes.TryIndex(channel, out var proto) && proto.Frequency == frequency);
    }

    private void UpdateUiState(EntityUid uid, EntityUid actor)
    {
        var state = new HeadsetChannelUiState(GetChannels(uid), GetMuted(actor));
        _ui.SetUiState(uid, HeadsetChannelUiKey.Key, state);
    }
}
