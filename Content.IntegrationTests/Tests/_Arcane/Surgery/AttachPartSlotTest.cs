// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;

namespace Content.IntegrationTests.Tests._Arcane.Surgery;

[TestFixture]
[TestOf(typeof(SharedSurgerySystem))]
public sealed class AttachPartSlotTest
{
    [Test]
    public async Task AttachedTailReturnsToItsSlot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var body = entMan.System<SharedBodySystem>();
        var surgery = entMan.System<SharedSurgerySystem>();

        await server.WaitAssertion(() =>
        {
            var carp = entMan.SpawnEntity("MobCarp", map.GridCoords);
            var tail = body.GetBodyChildrenOfType(carp, BodyPartType.Tail).First().Id;
            var (groin, slot) = body.GetParentPartAndSlotOrNull(tail)!.Value;
            Assert.That(body.DetachPart(groin, slot, tail));

            // The attach surgery names its slot "tail", which a name built from the symmetry would miss.
            var attach = surgery.GetSingleton("SurgeryAttachTail")!.Value;
            var step = surgery.GetSingleton("SurgeryStepInsertFeature")!.Value;
            var ev = new SurgeryStepEvent(carp, carp, groin, tail, attach, step, false);
            entMan.EventBus.RaiseLocalEvent(step, ref ev);

            Assert.That(body.GetParentPartAndSlotOrNull(tail), Is.EqualTo((groin, slot)));
        });

        await pair.CleanReturnAsync();
    }
}
