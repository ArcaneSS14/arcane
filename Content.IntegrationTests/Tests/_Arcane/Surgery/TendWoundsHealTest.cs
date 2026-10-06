// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage.Prototypes;

namespace Content.IntegrationTests.Tests._Arcane.Surgery;

[TestFixture]
[TestOf(typeof(SharedSurgerySystem))]
public sealed class TendWoundsHealTest
{
    [TestCase(1f)]
    [TestCase(0.5f)]
    public async Task TendingUntilNoWoundsClearsPartDamage(float woundMultiplier)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var body = entMan.System<SharedBodySystem>();
        var surgery = entMan.System<SharedSurgerySystem>();
        var damageable = entMan.System<DamageableSystem>();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var arm = body.GetBodyChildrenOfType(patient, BodyPartType.Arm, symmetry: BodyPartSymmetry.Right).First().Id;

            // Heavy melee attacks and explosions scale wound severity but not the damage itself.
            var hit = new DamageSpecifier(protoMan.Index<DamageTypePrototype>("Blunt"), 40);
            hit.WoundSeverityMultipliers["Blunt"] = woundMultiplier;
            damageable.TryChangeDamage(patient, hit, true, targetPart: TargetBodyPart.RightArm);

            // Same damage the brute tend step deals, until the step's own completion check passes.
            var heal = new DamageSpecifier(protoMan.Index<DamageGroupPrototype>("Brute"), -30);
            for (var i = 0; i < 50 && surgery.GetTendableDamage(arm, "Brute") > 0; i++)
            {
                damageable.TryChangeDamage(patient, heal, true, partMultiplier: 0.5f,
                    targetPart: TargetBodyPart.RightArm, ignoreBlockers: true);
            }

            Assert.That(entMan.GetComponent<DamageableComponent>(arm).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
        });

        await pair.CleanReturnAsync();
    }
}
