using Content.Shared._Arcane.Actions;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Arcane.UserInterface.Systems.Actions;

/// <summary>
///     Client side mirror of the action prototypes the local player pinned. The server owns the set, this only
///     keeps it around while the player changes bodies and asks for it again after reconnecting.
/// </summary>
public sealed class PinnedActionsSystem : EntitySystem
{
    private readonly HashSet<EntProtoId> _pinned = new();

    /// <summary>
    ///     Raised when the pinned actions changed and the action bar has to be rebuilt.
    /// </summary>
    public event Action? PinsChanged;

    public bool IsPinned(EntProtoId prototype)
    {
        return _pinned.Contains(prototype);
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PinnedActionsStateMessage>(OnState);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
    }

    /// <summary>
    ///     Pins or unpins an action prototype, the pin is kept across bodies and reconnects.
    /// </summary>
    public void SetPinned(EntProtoId prototype, bool pinned)
    {
        if (pinned)
        {
            if (!_pinned.Add(prototype))
                return;
        }
        else if (!_pinned.Remove(prototype))
        {
            return;
        }

        RaiseNetworkEvent(new SetPinnedActionMessage(prototype, pinned));
        PinsChanged?.Invoke();
    }

    private void OnState(PinnedActionsStateMessage args)
    {
        if (_pinned.SetEquals(args.Pinned))
            return;

        _pinned.Clear();
        _pinned.UnionWith(args.Pinned);

        PinsChanged?.Invoke();
    }

    /// <summary>
    ///     A client has no pins of its own, it asks the server for them whenever it starts playing a body. This
    ///     covers joining, changing bodies and reconnecting, and it resyncs a client that connected elsewhere.
    /// </summary>
    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        RaiseNetworkEvent(new RequestPinnedActionsMessage());
    }
}
