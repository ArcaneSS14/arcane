// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Shared.Preferences;
using Content.Shared.Traits;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Tests.Shared._Arcane.Preferences;

[TestFixture]
public sealed class TraitValidationTest : ContentUnitTest
{
    private const string TestPrototypes = @"
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

- type: trait
  id: TestSmallNegativeTrait
  name: trait-manic-name
  category: TestZeroPointTraits
  cost: -1
";

    private IPrototypeManager _prototypeManager = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        IoCManager.Resolve<ISerializationManager>().Initialize();
        _prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        _prototypeManager.Initialize();
        _prototypeManager.LoadString(TestPrototypes);
        _prototypeManager.ResolveResults();
    }

    [TestCase("TestPositiveTrait", "TestNegativeTrait")]
    [TestCase("TestNegativeTrait", "TestPositiveTrait")]
    public void NegativeTraitPaysForPositiveTraitInAnyOrder(string first, string second)
    {
        var traits = new ProtoId<TraitPrototype>[] { first, second };

        Assert.That(Validate(traits), Is.EquivalentTo(traits));
    }

    [Test]
    public void PositiveTraitOverLimitIsDropped()
    {
        var valid = Validate(new ProtoId<TraitPrototype>[] { "TestPositiveTrait", "TestSmallNegativeTrait" });

        Assert.That(valid, Is.EquivalentTo(new ProtoId<TraitPrototype>[] { "TestSmallNegativeTrait" }));
    }

    private List<ProtoId<TraitPrototype>> Validate(ProtoId<TraitPrototype>[] traits)
    {
        return new HumanoidCharacterProfile().GetValidTraits(traits, _prototypeManager);
    }
}
