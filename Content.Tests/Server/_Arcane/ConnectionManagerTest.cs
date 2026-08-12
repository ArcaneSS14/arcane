// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Connection;
using NUnit.Framework;

namespace Content.Tests.Server._Arcane;

[TestFixture, TestOf(typeof(ConnectionManager))]
[Parallelizable(ParallelScope.All)]
public static class ConnectionManagerTest
{
    [TestCase(false, false, false, false)]
    [TestCase(false, false, true, false)]
    [TestCase(false, true, false, false)]
    [TestCase(false, true, true, true)]
    [TestCase(true, false, false, true)]
    [TestCase(true, false, true, true)]
    [TestCase(true, true, false, true)]
    [TestCase(true, true, true, true)]
    public static void ExplicitJoinPrivilegePolicy(
        bool hasTemporaryBypass,
        bool isAdmin,
        bool adminBypassEnabled,
        bool expected)
    {
        Assert.That(
            ConnectionManager.HasExplicitJoinPrivilege(
                hasTemporaryBypass,
                isAdmin,
                adminBypassEnabled),
            Is.EqualTo(expected));
    }
}
