// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Network;

namespace Content.Goobstation.Server._Arcane.JoinQueue;

/// <summary>
/// Tracks the exact sessions admitted beyond the soft player limit.
/// </summary>
internal sealed class JoinQueueLimitBypassState<TSession> where TSession : class
{
    private readonly Dictionary<NetUserId, TSession> _sessionsByUser = [];

    public int Count => _sessionsByUser.Count;

    public void Add(NetUserId userId, TSession session)
    {
        _sessionsByUser[userId] = session;
    }

    public bool Contains(NetUserId userId, TSession session)
    {
        return _sessionsByUser.TryGetValue(userId, out var current) &&
               ReferenceEquals(current, session);
    }

    public bool Remove(NetUserId userId, TSession session)
    {
        if (!Contains(userId, session))
            return false;

        return _sessionsByUser.Remove(userId);
    }

    public bool Remove(NetUserId userId)
    {
        return _sessionsByUser.Remove(userId);
    }
}
