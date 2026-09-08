// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Server._Arcane.JoinQueue;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests._Arcane;

[TestFixture]
public sealed class JoinQueueStateTest
{
    [Test]
    public void PriorityPlayersStayFifoAheadOfRegularPlayers()
    {
        var queue = new JoinQueueState<string>();

        queue.Enqueue(CreateEntry("regular-1", 0));
        queue.Enqueue(CreateEntry("priority-1", 1, priority: true));
        queue.Enqueue(CreateEntry("regular-2", 2));
        queue.Enqueue(CreateEntry("priority-2", 3, priority: true));

        Assert.That(queue.Entries.Select(static entry => entry.Session), Is.EqualTo(new[]
        {
            "priority-1",
            "priority-2",
            "regular-1",
            "regular-2",
        }));
    }

    [Test]
    public void DisablingPriorityRestoresGlobalConnectionOrder()
    {
        var queue = new JoinQueueState<string>();
        queue.Enqueue(CreateEntry("regular", 0));
        queue.Enqueue(CreateEntry("priority", 1, priority: true));

        queue.SetPriorityEnabled(false);
        Assert.That(queue.Entries.Select(static entry => entry.Session), Is.EqualTo(new[]
        {
            "regular",
            "priority",
        }));

        queue.SetPriorityEnabled(true);
        Assert.That(queue.Entries.Select(static entry => entry.Session), Is.EqualTo(new[]
        {
            "priority",
            "regular",
        }));
    }

    [Test]
    public void RemovedUserReenqueuesAtTailWithNewOrder()
    {
        var queue = new JoinQueueState<string>();
        var userId = new NetUserId(Guid.NewGuid());

        queue.Enqueue(CreateEntry("returning-old", 0, userId: userId));
        queue.Enqueue(CreateEntry("other", 1));
        Assert.That(queue.TryRemove(userId, out _), Is.True);
        Assert.That(queue.Enqueue(CreateEntry("returning-new", 2, userId: userId)), Is.True);

        Assert.That(queue.Entries.Select(static entry => entry.Session), Is.EqualTo(new[]
        {
            "other",
            "returning-new",
        }));
    }

    [Test]
    public void UserCannotExistTwiceInQueue()
    {
        var queue = new JoinQueueState<string>();
        var userId = new NetUserId(Guid.NewGuid());

        Assert.That(queue.Enqueue(CreateEntry("original", 0, userId: userId)), Is.True);
        Assert.That(queue.Enqueue(CreateEntry("duplicate", 1, userId: userId)), Is.False);
        Assert.That(queue.Count, Is.EqualTo(1));
        Assert.That(queue.TryGet(userId, out var entry), Is.True);
        Assert.That(entry.Session, Is.EqualTo("original"));
    }

    [Test]
    public void StaleDisconnectCannotRemoveReplacementLimitBypass()
    {
        var state = new JoinQueueLimitBypassState<object>();
        var userId = new NetUserId(Guid.NewGuid());
        var oldSession = new object();
        var replacementSession = new object();

        state.Add(userId, oldSession);
        state.Add(userId, replacementSession);

        Assert.Multiple(() =>
        {
            Assert.That(state.Remove(userId, oldSession), Is.False);
            Assert.That(state.Contains(userId, replacementSession), Is.True);
            Assert.That(state.Count, Is.EqualTo(1));
        });
    }

    private static JoinQueueState<string>.Entry CreateEntry(
        string session,
        long order,
        bool priority = false,
        NetUserId? userId = null)
    {
        return new JoinQueueState<string>.Entry(
            userId ?? new NetUserId(Guid.NewGuid()),
            session,
            order,
            priority,
            TimeSpan.Zero);
    }
}
