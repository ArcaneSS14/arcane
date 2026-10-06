// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client._Arcane.Medical.Surgery;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Log;

namespace Content.IntegrationTests.Tests._Arcane.Surgery;

[TestFixture]
[TestOf(typeof(SurgeryBui))]
public sealed class SurgeryChartTest : InteractionTest
{
    [Test]
    public async Task AttachedLegShowsWithoutReopening()
    {
        // The doll loads raw PNGs from inside an RSI, which the engine warns about.
        Pair.ClientLogHandler.FailureLevel = LogLevel.Error;

        await SpawnTarget("MobHuman");
        var patient = STarget!.Value;
        var body = SEntMan.System<SharedBodySystem>();
        var surgery = SEntMan.System<SharedSurgerySystem>();
        var ui = SEntMan.System<SharedUserInterfaceSystem>();
        EntityUid leg = default;
        EntityUid groin = default;
        var slot = string.Empty;

        await Server.WaitAssertion(() =>
        {
            var targeting = SEntMan.EnsureComponent<TargetingComponent>(SPlayer);
            targeting.Target = TargetBodyPart.Groin;
            SEntMan.Dirty(SPlayer, targeting);

            leg = body.GetBodyChildrenOfType(patient, BodyPartType.Leg, symmetry: BodyPartSymmetry.Left).Single().Id;
            (groin, slot) = body.GetParentPartAndSlotOrNull(leg)!.Value;
            SEntMan.System<WoundSystem>().AmputateWoundableSafely(groin, leg);
            Assert.That(HandSys.TryPickup(SPlayer, leg, Hands!.ActiveHandId, false, false, false, Hands));

            ui.OpenUi(patient, SurgeryUIKey.Key, SPlayer);
        });

        await RunSeconds(1);
        var legName = SEntMan.GetComponent<MetaDataComponent>(leg).EntityName;
        var window = GetWindow<SurgeryWindow>();
        Assert.That(DollShows(window, legName), Is.False);

        // The empty slot is found by its symmetry, so the severed leg stays on the doll as missing.
        await Client.WaitAssertion(() =>
        {
            var missing = Loc.GetString("surgery-ui-part-missing", ("part", Loc.GetString("surgery-ui-part-leftleg")));
            Assert.That(DollShows(window, missing), "The doll does not show the empty leg slot.");
        });

        // Like the insert step: the part is attached and the chart state is sent in the same tick.
        await Server.WaitAssertion(() =>
        {
            Assert.That(body.AttachPart(groin, slot, leg));
            SEntMan.EnsureComponent<BodyPartReattachedComponent>(leg);

            var choices = body.GetBodyChildren(patient)
                .ToDictionary(part => SEntMan.GetNetEntity(part.Id), part => surgery.GetValidSurgeries(patient, part.Id));
            ui.SetUiState(patient, SurgeryUIKey.Key, new SurgeryBuiState(choices));
        });

        // Only the client runs on, so no later state can bring the leg in.
        await Server.WaitRunTicks(1);
        await Client.WaitRunTicks(30);

        Assert.That(DollShows(window, legName), "The doll does not show the attached leg.");
    }

    // Doll hit areas carry the part tooltip, which starts with the part name.
    private static bool DollShows(SurgeryWindow window, string partName)
    {
        return window.Doll.Children.Any(area => area.Visible && area.ToolTip?.Split('\n')[0] == partName);
    }
}
