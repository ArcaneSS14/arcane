// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.IO;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Kitchen.Components;
using Content.Server.Kitchen.EntitySystems;
using Content.Shared.Kitchen;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using YamlDotNet.RepresentationModel;

namespace Content.Tests.Server.Kitchen;

[TestFixture, TestOf(typeof(FoodRecipePrototype))]
public sealed class MicrowaveRecipeTest : ContentUnitTest
{
    private static bool _serializationInitialized;

    [Test]
    public void AllocationDoesNotDependOnRequirementOrder()
    {
        var solids = new List<MicrowaveSystem.MicrowaveSolid>
        {
            new(default, "SubstituteAB", 1, new List<string> { "TestIngredientA", "TestIngredientB" }),
            new(default, "SubstituteA", 1, new List<string> { "TestIngredientA" }),
        };

        var component = new MicrowaveComponent { CurrentCookTimerTime = 5 };

        var ab = ReadRecipe("    TestIngredientA: 1\n    TestIngredientB: 1\n");
        var ba = ReadRecipe("    TestIngredientB: 1\n    TestIngredientA: 1\n");

        Assert.Multiple(() =>
        {
            Assert.That(GetPortions(component, ab, solids), Is.EqualTo(1));
            Assert.That(GetPortions(component, ba, solids), Is.EqualTo(1));
        });
    }

    [Test]
    public void FractionalSolidRequirementRoundsUp()
    {
        var recipe = ReadRecipe("    TestIngredientA: 2.5\n");
        var component = new MicrowaveComponent { CurrentCookTimerTime = 5 };

        var two = new List<MicrowaveSystem.MicrowaveSolid> { new(default, "TestIngredientA", 2, null) };
        var three = new List<MicrowaveSystem.MicrowaveSolid> { new(default, "TestIngredientA", 3, null) };

        Assert.Multiple(() =>
        {
            Assert.That(GetPortions(component, recipe, two), Is.EqualTo(0));
            Assert.That(GetPortions(component, recipe, three), Is.EqualTo(1));
        });
    }

    private static int GetPortions(
        MicrowaveComponent component,
        FoodRecipePrototype recipe,
        List<MicrowaveSystem.MicrowaveSolid> solids)
    {
        return MicrowaveSystem.CanSatisfyRecipe(component, recipe, solids, new Dictionary<string, FixedPoint2>()).Item2;
    }

    private static FoodRecipePrototype ReadRecipe(string solids)
    {
        using var stream = new StringReader($@"
- type: microwaveMealRecipe
  id: TestRecipe
  name: test-recipe
  result: TestFoodResult
  time: 5
  solids:
{solids}");

        var yamlStream = new YamlStream();
        yamlStream.Load(stream);

        var rootNode = (YamlSequenceNode) yamlStream.Documents[0].RootNode;
        var proto = (YamlMappingNode) rootNode[0];

        var serializationManager = IoCManager.Resolve<ISerializationManager>();

        if (!_serializationInitialized)
        {
            serializationManager.Initialize();
            _serializationInitialized = true;
        }

        return serializationManager.Read<FoodRecipePrototype>(new MappingDataNode(proto));
    }
}
