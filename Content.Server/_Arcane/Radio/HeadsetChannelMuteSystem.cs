using System.Linq;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Verbs;
using Content.Shared._Arcane.Radio;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Robust.Server.GameObjects;

namespace Content.Server._Arcane.Radio;

/// <summary>
///     Lets a player mute receiving radio messages per channel through a BUI on a headset.
///     The muted set is per player session, so only delivery to that player is skipped;
///     the player can still broadcast on the same frequencies.
/// </summary>
public sealed class HeadsetChannelMuteSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private readonly Dictionary<NetUserId, HashSet<int>> _mutedFrequencies = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<HeadsetComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<HeadsetComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<HeadsetComponent, HeadsetChannelMuteMessage>(OnToggleMute);
        SubscribeLocalEvent<HeadsetComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    public override void Shutdown()
    {
        _mutedFrequencies.Clear();
        base.Shutdown();
    }

    public bool IsMuted(NetUserId userId, int frequency)
    {
        return _mutedFrequencies.TryGetValue(userId, out var muted) && muted.Contains(frequency);
    }

    private void OnComponentInit(Entity<HeadsetComponent> ent, ref ComponentInit args)
    {
        _ui.SetUi(ent.Owner, HeadsetChannelUiKey.Key,
            new InterfaceData("HeadsetChannelBoundUserInterface", interactionRange: 0, requireInputValidation: false));
    }

    private void OnGetVerbs(EntityUid uid, HeadsetComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

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

        UpdateUiState(ent, args.Actor);
    }

    private void OnToggleMute(Entity<HeadsetComponent> ent, ref HeadsetChannelMuteMessage args)
    {
        if (args.Frequency <= 0 || !TryComp<ActorComponent>(args.Actor, out var actor))
            return;

        if (!_mutedFrequencies.TryGetValue(actor.PlayerSession.UserId, out var muted))
            _mutedFrequencies[actor.PlayerSession.UserId] = muted = new HashSet<int>();

        if (args.Muted)
            muted.Add(args.Frequency);
        else
            muted.Remove(args.Frequency);

        UpdateUiState(ent, args.Actor);
    }

    private HashSet<int> GetMuted(EntityUid actor)
    {
        if (TryComp<ActorComponent>(actor, out var actorComp)
            && _mutedFrequencies.TryGetValue(actorComp.PlayerSession.UserId, out var muted))
            return muted;

        return new();
    }

    private void UpdateUiState(Entity<HeadsetComponent> ent, EntityUid actor)
    {
        if (!TryComp<EncryptionKeyHolderComponent>(ent.Owner, out var keys))
            return;

        var state = new HeadsetChannelUiState(keys.Channels.ToList(), GetMuted(actor));
        _ui.SetUiState(ent.Owner, HeadsetChannelUiKey.Key, state);
    }
}
