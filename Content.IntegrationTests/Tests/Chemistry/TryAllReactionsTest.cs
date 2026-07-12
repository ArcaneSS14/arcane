// SPDX-FileCopyrightText: 2021 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2021 Javier Guardia Fernández <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2021 Paul <ritter.paul1+git@googlemail.com>
// SPDX-FileCopyrightText: 2021 Paul Ritter <ritter.paul1@googlemail.com>
// SPDX-FileCopyrightText: 2021 Vera Aguilera Puerto <gradientvera@outlook.com>
// SPDX-FileCopyrightText: 2021 Ygg01 <y.laughing.man.y@gmail.com>
// SPDX-FileCopyrightText: 2021 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2022 0x6273 <0x40@keemail.me>
// SPDX-FileCopyrightText: 2022 Timothy Teakettle <59849408+timothyteakettle@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 metalgearsloth <metalgearsloth@gmail.com>
// SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2022 wrexbe <81056464+wrexbe@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 ElectroJr <leonsfriedrich@gmail.com>
// SPDX-FileCopyrightText: 2023 Emisse <99158783+Emisse@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 TemporalOroboros <TemporalOroboros@gmail.com>
// SPDX-FileCopyrightText: 2023 Visne <39844191+Visne@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Aidenkrz <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2024 Tayrtahn <tayrtahn@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.IntegrationTests.Tests.Chemistry
{
    [TestFixture]
    [TestOf(typeof(ReactionPrototype))]
    public sealed class TryAllReactionsTest
    {
        [TestPrototypes]
        private const string Prototypes = @"
- type: entity
  id: TestSolutionContainer
  components:
  - type: SolutionContainerManager
    solutions:
      beaker:
        maxVol: 500
        canMix: true"; // Orion-Edit: 50 > 500

        [Test]
        public async Task TryAllTest()
        {
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;

            var entityManager = server.ResolveDependency<IEntityManager>();
            var prototypeManager = server.ResolveDependency<IPrototypeManager>();
            var testMap = await pair.CreateTestMap();
            var coordinates = testMap.GridCoords;
            var solutionContainerSystem = entityManager.System<SharedSolutionContainerSystem>();

            foreach (var reactionPrototype in prototypeManager.EnumeratePrototypes<ReactionPrototype>())
            {
                //since i have no clue how to isolate each loop assert-wise im just gonna throw this one in for good measure
                Console.WriteLine($"Testing {reactionPrototype.ID}");

                EntityUid beaker = default;
                Entity<SolutionComponent>? solutionEnt = default!;
                Solution solution = null;

                await server.WaitAssertion(() =>
                {
                    beaker = entityManager.SpawnEntity("TestSolutionContainer", coordinates);
                    Assert.That(solutionContainerSystem
                        .TryGetSolution(beaker, "beaker", out solutionEnt, out solution));
                    foreach (var (id, reactant) in reactionPrototype.Reactants)
                    {
                        // Arcane-Start: Add reagents and set temperature directly on the solution instance, bypassing TryAddReagent/SetTemperature, both of which internally call UpdateChemicals(soln) on every single call. That used to let passive temperature-only reactions (e.g. WaterFreezing) consume reagents *before* the mixer-only reaction under test got a chance to run, producing false negatives like the Vodka reaction failing because its Water reactant froze into Ice mid-setup.
                        var accepted = solution.AvailableVolume > reactant.Amount
                            ? reactant.Amount
                            : solution.AvailableVolume;
                        solution.AddReagent(id, accepted, dirtyHeatCap: false);
                        // Arcane-End
#pragma warning disable NUnit2045
                        /* Arcane-Edit-Start
                        Assert.That(solutionContainerSystem
                            .TryAddReagent(solutionEnt.Value, id, reactant.Amount, out var quantity));
                        */ // Arcane-Edit-End
                        Assert.That(reactant.Amount, Is.EqualTo(accepted)); // Arcane-Edit
#pragma warning restore NUnit2045
                    }
                    // Goobstation - this part of the test disallows many interesting temperature dependent chemical reactions
                    /*
                    //Get all possible reactions with the current reagents
                    var possibleReactions = prototypeManager.EnumeratePrototypes<ReactionPrototype>()
                        .Where(x => x.Reactants.All(id => solution.Contents.Any(s => s.Reagent.Prototype == id.Key)))
                        .ToList();

                    //Check if the reaction is the first to occur when heated
                    foreach (var possibleReaction in possibleReactions.OrderBy(r => r.MinimumTemperature))
                    {
                        if (possibleReaction.MinimumTemperature < reactionPrototype.MinimumTemperature && possibleReaction.MixingCategories == reactionPrototype.MixingCategories)
                        {
                            Assert.Fail($"The {possibleReaction.ID} reaction may occur before {reactionPrototype.ID} when heated.");
                        }
                    }

                    //Check if the reaction is the first to occur when freezing
                    foreach (var possibleReaction in possibleReactions.OrderBy(r => r.MaximumTemperature))
                    {
                        if (possibleReaction.MaximumTemperature > reactionPrototype.MaximumTemperature && possibleReaction.MixingCategories == reactionPrototype.MixingCategories)
                        {
                            Assert.Fail($"The {possibleReaction.ID} reaction may occur before {reactionPrototype.ID} when freezing.");
                        }
                    }
                    */
                    //Now safe set the temperature and mix the reagents
                    solution.Temperature = reactionPrototype.MinimumTemperature; // Arcane-Edit

                    ReactionMixerComponent? mixerComponent = null; // Arcane
                    if (reactionPrototype.MixingCategories != null)
                    {
                        var dummyEntity = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
                        mixerComponent = entityManager.AddComponent<ReactionMixerComponent>(dummyEntity); // Arcane-Edit
                        mixerComponent.ReactionTypes = reactionPrototype.MixingCategories;
                        solutionContainerSystem.UpdateChemicals(solutionEnt.Value, true, mixerComponent);
                    }
                    solutionContainerSystem.UpdateChemicals(solutionEnt.Value, true, mixerComponent); // Arcane: Single reaction-processing pass, with reagents + temperature + mixer already fully set up.
                });

                await server.WaitIdleAsync();

                await server.WaitAssertion(() =>
                {
                    //you just got linq'd fool
                    //(i'm sorry)
                    var foundProductsMap = reactionPrototype.Products
                        .Concat(reactionPrototype.Reactants.Where(x => x.Value.Catalyst).ToDictionary(x => x.Key, x => x.Value.Amount))
                        .ToDictionary(x => x, _ => false);
                    foreach (var (reagent, quantity) in solution.Contents)
                    {
                        Assert.That(foundProductsMap.TryFirstOrNull(x => x.Key.Key == reagent.Prototype && x.Key.Value == quantity, out var foundProduct));
                        foundProductsMap[foundProduct.Value.Key] = true;
                    }

                    Assert.That(foundProductsMap.All(x => x.Value));
                });

            }
            await pair.CleanReturnAsync();
        }
    }

}
