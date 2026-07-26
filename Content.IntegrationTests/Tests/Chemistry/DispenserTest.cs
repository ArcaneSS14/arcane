// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Chemistry.UI;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;

namespace Content.IntegrationTests.Tests.Chemistry;

public sealed class DispenserTest : InteractionTest
{
    /// <summary>
    ///     Basic test that checks that a beaker can be inserted and ejected from a dispenser.
    /// </summary>
    [Test]
    public async Task InsertEjectBuiTest()
    {
        await SpawnTarget("ChemDispenser");
        ToggleNeedPower();

        // Insert beaker
        await InteractUsing("Beaker");
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);

        // Open BUI
        await Interact();

        // Eject beaker via BUI.
        var ev = new ItemSlotButtonPressedEvent(SharedReagentDispenser.OutputSlotName);
        await SendBui(ReagentDispenserUiKey.Key, ev);

        // Beaker is back in the player's hands
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Not.Null);
        AssertPrototype("Beaker", SEntMan.GetNetEntity(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands))));

        // Re-insert the beaker
        await InteractUsing("Beaker"); // Orion-Edit: InteractUsing Beaker
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);

        // Orion-Start
        // Re-open BUI
        await Interact();

        // Eject again
        await SendBui(ReagentDispenserUiKey.Key, new ItemSlotButtonPressedEvent(SharedReagentDispenser.OutputSlotName));
        await RunTicks(5);
        // Orion-End

        // Now click the eject button directly
        await ClickControl<ReagentDispenserWindow>(nameof(ReagentDispenserWindow.EjectButton));
        await RunTicks(5);

        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Not.Null);
        AssertPrototype("Beaker", SEntMan.GetNetEntity(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands))));
    }
}
