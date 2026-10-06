// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;

namespace Content.IntegrationTests.Tests._Arcane.Surgery;

[TestFixture]
[TestOf(typeof(WoundSystem))]
public sealed class ReattachedPartSeverityTest
{
    [Test]
    public async Task ReattachedArmIsNotSevered()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var body = entMan.System<SharedBodySystem>();
        var wounds = entMan.System<WoundSystem>();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var (arm, armPart) = body.GetBodyChildrenOfType(patient, BodyPartType.Arm, symmetry: BodyPartSymmetry.Right).First();
            var (parent, slot) = body.GetParentPartAndSlotOrNull(arm)!.Value;

            wounds.AmputateWoundableSafely(parent, arm);
            var woundable = entMan.GetComponent<WoundableComponent>(arm);
            Assert.That(woundable.WoundableSeverity, Is.EqualTo(WoundableSeverity.Severed));

            Assert.That(body.TryCreatePartSlotAndAttach(parent, slot, arm, armPart.PartType, armPart.Symmetry));
            Assert.That(woundable.WoundableSeverity, Is.Not.EqualTo(WoundableSeverity.Severed));
        });

        await pair.CleanReturnAsync();
    }
}
