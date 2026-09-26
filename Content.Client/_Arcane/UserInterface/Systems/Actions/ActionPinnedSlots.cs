// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Content.Client.UserInterface.Systems.Actions;

namespace Content.Client._Arcane.UserInterface.Systems.Actions;

/// <summary>
///     Client-side tracking of pinned action bar slots. A pinned action keeps its slot when it is removed
///     from the action bar and is restored into that same slot once a matching action becomes available again.
/// </summary>
internal sealed class ActionPinnedSlots
{
    private readonly Dictionary<EntityUid, PinData> _pins = new();
    private readonly HashSet<EntityUid> _unavailable = new();

    public bool IsPinned(EntityUid action)
    {
        return _pins.ContainsKey(action);
    }

    public bool IsUnavailable(EntityUid action)
    {
        return _unavailable.Contains(action);
    }

    public void Pin(EntityUid action, EntProtoId? prototype, EntityUid? container)
    {
        _pins[action] = new PinData(prototype, container);
        _unavailable.Remove(action);
    }

    /// <summary>
    ///     Drops the pin of an action.
    /// </summary>
    /// <returns>True if the action was a placeholder for a currently unavailable action.</returns>
    public bool Unpin(EntityUid action)
    {
        _pins.Remove(action);
        return _unavailable.Remove(action);
    }

    /// <summary>
    ///     Marks a pinned action as unavailable, keeping it in its slot.
    /// </summary>
    public void SetUnavailable(EntityUid action)
    {
        if (_pins.ContainsKey(action))
            _unavailable.Add(action);
    }

    /// <summary>
    ///     Moves pin state to a different action entity, e.g. after the action bar was rebuilt with new entities.
    /// </summary>
    public void Remap(EntityUid from, EntityUid to)
    {
        if (from == to)
            return;

        if (_pins.Remove(from, out var pin))
            _pins[to] = pin;

        if (_unavailable.Remove(from))
            _unavailable.Add(to);
    }

    public void Clear()
    {
        _pins.Clear();
        _unavailable.Clear();
    }

    /// <summary>
    ///     Tries to put a newly added action back into the slot of an unavailable pinned action.
    /// </summary>
    /// <returns>True if the action was restored into a pinned slot.</returns>
    public bool TryRestore(EntityUid action, EntProtoId? prototype, EntityUid? container, IList<EntityUid?> slots)
    {
        if (TryRestoreSlot(action, prototype, container, slots, true))
            return true;

        // An item can move between containers while it is not held, e.g. from a hand to the floor,
        // so the prototype alone decides when the action is the one a slot is waiting for.
        return TryRestoreSlot(action, prototype, container, slots, false);
    }

    private bool TryRestoreSlot(
        EntityUid action,
        EntProtoId? prototype,
        EntityUid? container,
        IList<EntityUid?> slots,
        bool requireContainer)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i] is not { } slotId || !_unavailable.Contains(slotId) || !_pins.TryGetValue(slotId, out var pin))
                continue;

            if (pin.Prototype != prototype)
                continue;

            if (requireContainer && pin.Container != null && pin.Container != container)
                continue;

            slots[i] = action;
            _pins.Remove(slotId);
            _pins[action] = pin;
            _unavailable.Remove(slotId);
            _unavailable.Remove(action);
            return true;
        }

        return false;
    }

    private readonly record struct PinData(EntProtoId? Prototype, EntityUid? Container);
}
