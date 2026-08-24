using System;
using Content.Goobstation.Shared.Xenomorph;
using NUnit.Framework;

namespace Content.Tests.Shared;

[TestFixture]
public sealed class XenoInstantGrabTest
{
    [Test]
    public void XenoInstantGrabStateTest()
    {
        var comp = new XenoInstantGrabComponent
        {
            NextInstantGrab = TimeSpan.FromSeconds(10)
        };

        Assert.That(comp.NextInstantGrab, Is.EqualTo(TimeSpan.FromSeconds(10)));
    }
}
