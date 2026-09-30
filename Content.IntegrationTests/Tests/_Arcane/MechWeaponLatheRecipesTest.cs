// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Lathe;
using Content.Server.Research.Systems;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Arcane;

[TestFixture]
public sealed class MechWeaponLatheRecipesTest
{
    private static readonly EntProtoId ResearchServer = "ResearchAndDevelopmentServer";
    private static readonly EntProtoId SecurityTechFab = "SecurityTechFab";
    private static readonly EntProtoId ExosuitFabricator = "ExosuitFabricator";

    private static readonly ProtoId<LatheRecipePrototype>[] MechWeapons =
    [
        "WeaponMechChainSword",
        "WeaponMechCombatDisabler",
        "WeaponMechCombatFiredartLaser",
        "WeaponMechCombatFlashbangLauncher",
        "WeaponMechCombatImmolationGun",
        "WeaponMechCombatMissileRack6",
        "WeaponMechCombatMissileRack8",
        "WeaponMechCombatShotgun",
        "WeaponMechCombatShotgunIncendiary",
        "WeaponMechCombatSolarisLaser",
        "WeaponMechCombatTeslaCannon",
        "WeaponMechCombatUltraRifle",
    ];

    [Test]
    public async Task MechWeaponsAvailability()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid secFab = default;
        EntityUid exoFab = default;

        await server.WaitPost(() =>
        {
            secFab = server.EntMan.SpawnEntity(SecurityTechFab, map.GridCoords);
            exoFab = server.EntMan.SpawnEntity(ExosuitFabricator, map.GridCoords);
            var researchServer = server.EntMan.SpawnEntity(ResearchServer, map.GridCoords);
            server.System<ResearchSystem>().UnlockAllTechnologiesOnServer(researchServer);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var lathe = server.System<LatheSystem>();
            var secRecipes = lathe.GetAvailableRecipes(secFab, server.EntMan.GetComponent<LatheComponent>(secFab));
            var exoComp = server.EntMan.GetComponent<LatheComponent>(exoFab);
            var exoRecipes = lathe.GetAvailableRecipes(exoFab, exoComp);

            server.EntMan.EnsureComponent<EmaggedComponent>(exoFab).EmagType = EmagType.Interaction;
            var exoEmagRecipes = lathe.GetAvailableRecipes(exoFab, exoComp);

            Assert.Multiple(() =>
            {
                foreach (var weapon in MechWeapons)
                {
                    Assert.That(secRecipes, Does.Contain(weapon), $"{weapon} is missing in {SecurityTechFab}.");
                    Assert.That(exoRecipes, Does.Not.Contain(weapon), $"{weapon} is available in non-emagged {ExosuitFabricator}.");
                    Assert.That(exoEmagRecipes, Does.Contain(weapon), $"{weapon} is missing in emagged {ExosuitFabricator}.");
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
