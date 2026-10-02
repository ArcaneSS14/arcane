// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Preferences;
using Content.Shared.Traits;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Arcane.Preferences;

[TestFixture]
public sealed class TraitValidationTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: traitCategory
  id: TestZeroPointTraits
  name: trait-category-mood
  maxTraitPoints: 0

- type: trait
  id: TestPositiveTrait
  name: trait-sanguine-name
  category: TestZeroPointTraits
  cost: 3

- type: trait
  id: TestNegativeTrait
  name: trait-saturnine-name
  category: TestZeroPointTraits
  cost: -3
";

    [Test]
    public async Task NegativeTraitPaysForPositiveTraitListedBeforeIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var traits = new ProtoId<TraitPrototype>[] { "TestPositiveTrait", "TestNegativeTrait" };
            var valid = new HumanoidCharacterProfile().GetValidTraits(traits, protoManager);

            Assert.That(valid, Is.EquivalentTo(traits));
        });

        await pair.CleanReturnAsync();
    }
}
