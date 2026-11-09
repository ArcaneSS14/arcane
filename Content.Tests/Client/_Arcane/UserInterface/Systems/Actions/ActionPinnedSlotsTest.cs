using System.Collections.Generic;
using Content.Client._Arcane.UserInterface.Systems.Actions;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Tests.Client._Arcane.UserInterface.Systems.Actions;

[TestFixture]
[TestOf(typeof(ActionPinnedSlots))]
public sealed class ActionPinnedSlotsTest
{
    private static readonly EntProtoId Prototype = "ActionToggleInternals";

    private static readonly EntityUid Boots = new(1);
    private static readonly EntityUid Tank = new(2);
    private static readonly EntityUid BootsAction = new(3);
    private static readonly EntityUid TankAction = new(4);

    [Test]
    public void ReturningActionKeepsItsOwnSlot()
    {
        var pins = new ActionPinnedSlots();
        pins.Pin(TankAction, Prototype, null);
        pins.Pin(BootsAction, Prototype, Boots);
        pins.SetUnavailable(TankAction);
        pins.SetUnavailable(BootsAction);

        var slots = new List<EntityUid?> { TankAction, BootsAction };

        Assert.Multiple(() =>
        {
            Assert.That(pins.TryRestore(BootsAction, Prototype, Boots, slots), Is.True);
            Assert.That(slots, Is.EqualTo(new EntityUid?[] { TankAction, BootsAction }));
            Assert.That(pins.IsUnavailable(BootsAction), Is.False);
            Assert.That(pins.IsUnavailable(TankAction), Is.True);
        });
    }

    [Test]
    public void AvailableActionIsNotMoved()
    {
        var pins = new ActionPinnedSlots();
        pins.Pin(TankAction, Prototype, Tank);
        pins.SetUnavailable(TankAction);

        var slots = new List<EntityUid?> { TankAction, BootsAction };

        Assert.Multiple(() =>
        {
            Assert.That(pins.TryRestore(BootsAction, Prototype, Boots, slots), Is.False);
            Assert.That(slots, Is.EqualTo(new EntityUid?[] { TankAction, BootsAction }));
        });
    }

    [Test]
    public void OtherItemTakesFreePinnedSlot()
    {
        var pins = new ActionPinnedSlots();
        pins.Pin(BootsAction, Prototype, Boots);
        pins.SetUnavailable(BootsAction);

        var slots = new List<EntityUid?> { BootsAction };

        Assert.Multiple(() =>
        {
            Assert.That(pins.TryRestore(TankAction, Prototype, Tank, slots), Is.True);
            Assert.That(slots, Is.EqualTo(new EntityUid?[] { TankAction }));
            Assert.That(pins.IsPinned(TankAction), Is.True);
            Assert.That(pins.IsPinned(BootsAction), Is.False);
        });
    }
}
