using Content.Shared._Arcane.Actions;
using Content.Shared.Players;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Actions;

/// <summary>
///     Keeps the action prototypes a player pinned in their action bar. The server owns this data so that pins
///     survive changing bodies and reconnecting, the client only mirrors it into its action bar.
/// </summary>
public sealed class PinnedActionsSystem : EntitySystem
{
    /// <summary>
    ///     Guards against a client filling the session data with prototypes. A player can never pin more actions
    ///     than they have slots, so this is only a limit for the protocol.
    /// </summary>
    private const int MaxPinned = 32;

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<SetPinnedActionMessage>(OnSetPinned);
        SubscribeNetworkEvent<RequestPinnedActionsMessage>(OnRequest);
    }

    private void OnSetPinned(SetPinnedActionMessage msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (session?.Data.ContentData() is not { } data)
            return;

        if (!_prototypeManager.HasIndex(msg.Prototype))
        {
            // For special situations
            Log.Error($"Client {session.UserId} tried to pin unknown prototype {msg.Prototype}");
            return;
        }

        if (msg.Pinned)
        {
            if (data.PinnedActions.Count >= MaxPinned)
                return;

            data.PinnedActions.Add(msg.Prototype);
        }
        else
        {
            data.PinnedActions.Remove(msg.Prototype);
        }

        // Sending the state back also tells the client when a pin was rejected. The set is copied because the
        // session data keeps changing while the message is on the wire.
        RaiseNetworkEvent(new PinnedActionsStateMessage(new HashSet<EntProtoId>(data.PinnedActions)), session.Channel);
    }

    private void OnRequest(RequestPinnedActionsMessage msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (session == null)
            return;

        var pinned = session.Data.ContentData()?.PinnedActions;
        RaiseNetworkEvent(new PinnedActionsStateMessage(pinned == null ? new HashSet<EntProtoId>() : new HashSet<EntProtoId>(pinned)), session.Channel);
    }
}
