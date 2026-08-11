// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Connection;
using NUnit.Framework;

namespace Content.Tests.Server._Arcane;

[TestFixture, TestOf(typeof(ConnectionManager))]
[Parallelizable(ParallelScope.All)]
public static class ConnectionManagerTest
{
    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public static void AdminPlayerLimitPolicy(
        bool isAdmin,
        bool bypassEnabled,
        bool expected)
    {
        Assert.That(
            ConnectionManager.CanAdminBypassPlayerLimit(isAdmin, bypassEnabled),
            Is.EqualTo(expected));
    }
}
