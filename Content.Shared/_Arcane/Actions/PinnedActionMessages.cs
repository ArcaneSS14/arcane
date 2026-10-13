using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.Actions;

/// <summary>
///     Sent by a client when the player pins or unpins an action in their action bar. Pins are sent by
///     prototype, so that they can be restored for any body the player takes.
/// </summary>
[Serializable, NetSerializable]
public sealed class SetPinnedActionMessage(EntProtoId prototype, bool pinned) : EntityEventArgs
{
    public EntProtoId Prototype { get; } = prototype;
    public bool Pinned { get; } = pinned;
}

/// <summary>
///     Sent by a client that has no pinned actions yet, e.g. after joining the server or reconnecting.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestPinnedActionsMessage : EntityEventArgs;

/// <summary>
///     The authoritative set of action prototypes a player has pinned.
/// </summary>
[Serializable, NetSerializable]
public sealed class PinnedActionsStateMessage(HashSet<EntProtoId> pinned) : EntityEventArgs
{
    public HashSet<EntProtoId> Pinned { get; } = pinned;
}
