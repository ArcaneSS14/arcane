// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Goobstation.Server._Arcane.JoinQueue;

/// <summary>
/// Keeps the ordered queue and its user lookup as one invariant.
/// </summary>
internal sealed class JoinQueueState<TSession> where TSession : notnull
{
    private readonly List<Entry> _entries = [];
    private readonly Dictionary<NetUserId, Entry> _entriesByUser = [];

    public IReadOnlyList<Entry> Entries => _entries;
    public int Count => _entries.Count;

    public bool PriorityEnabled { get; private set; } = true;

    public bool Contains(NetUserId userId)
    {
        return _entriesByUser.ContainsKey(userId);
    }

    public bool TryGet(NetUserId userId, out Entry entry)
    {
        return _entriesByUser.TryGetValue(userId, out entry!);
    }

    public bool Enqueue(Entry entry)
    {
        if (!_entriesByUser.TryAdd(entry.UserId, entry))
            return false;

        _entries.Insert(GetInsertionIndex(entry), entry);
        return true;
    }

    public bool TryRemove(NetUserId userId, out Entry entry)
    {
        if (!_entriesByUser.Remove(userId, out entry!))
            return false;

        var removed = _entries.Remove(entry);
        DebugTools.Assert(removed, "Queue lookup and ordered entries must stay synchronized.");
        return true;
    }

    public bool TryDequeue(out Entry entry)
    {
        if (_entries.Count == 0)
        {
            entry = default!;
            return false;
        }

        entry = _entries[0];
        _entries.RemoveAt(0);
        var removed = _entriesByUser.Remove(entry.UserId);
        DebugTools.Assert(removed, "Queue lookup and ordered entries must stay synchronized.");
        return true;
    }

    public void SetPriorityEnabled(bool enabled)
    {
        if (PriorityEnabled == enabled)
            return;

        PriorityEnabled = enabled;
        _entries.Sort(Compare);
    }

    public void Clear()
    {
        _entries.Clear();
        _entriesByUser.Clear();
    }

    private int GetInsertionIndex(Entry entry)
    {
        var low = 0;
        var high = _entries.Count;

        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (Compare(_entries[middle], entry) <= 0)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }

    private int Compare(Entry left, Entry right)
    {
        if (PriorityEnabled && left.IsPriority != right.IsPriority)
            return left.IsPriority ? -1 : 1;

        return left.Order.CompareTo(right.Order);
    }

    internal sealed record Entry(
        NetUserId UserId,
        TSession Session,
        long Order,
        bool IsPriority,
        TimeSpan WaitStartedAt);
}
