// SPDX-License-Identifier: MIT
// Arcane - gradient.

using System.Collections.Generic;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.Preferences.Humanoid;

[TestFixture]
public sealed class HairGradientTest
{
    [Test]
    public void TestHairGradientEquality()
    {
        var markings = new List<Marking>();
        var colors1 = new List<Color> { Color.Red, Color.Green };
        var colors2 = new List<Color> { Color.Red, Color.Green };
        var colors3 = new List<Color> { Color.Red, Color.Purple };

        var app1 = new HumanoidCharacterAppearance(
            "HairBald", Color.Black, "FacialHairClean", Color.Black, Color.Brown, Color.White,
            markings, true, colors1, HairGradientStyle.Split, 0.4f);

        var app2 = new HumanoidCharacterAppearance(
            "HairBald", Color.Black, "FacialHairClean", Color.Black, Color.Brown, Color.White,
            markings, true, colors2, HairGradientStyle.Split, 0.4f);

        var app3 = new HumanoidCharacterAppearance(
            "HairBald", Color.Black, "FacialHairClean", Color.Black, Color.Brown, Color.White,
            markings, true, colors3, HairGradientStyle.Split, 0.4f);

        var appDiffStyle = new HumanoidCharacterAppearance(
            "HairBald", Color.Black, "FacialHairClean", Color.Black, Color.Brown, Color.White,
            markings, true, colors1, HairGradientStyle.Underdye, 0.4f);

        var appDiffOffset = new HumanoidCharacterAppearance(
            "HairBald", Color.Black, "FacialHairClean", Color.Black, Color.Brown, Color.White,
            markings, true, colors1, HairGradientStyle.Split, 0.8f);

        Assert.That(app1.MemberwiseEquals(app2), Is.True);
        Assert.That(app1.MemberwiseEquals(app3), Is.False);
        Assert.That(app1.MemberwiseEquals(appDiffStyle), Is.False);
        Assert.That(app1.MemberwiseEquals(appDiffOffset), Is.False);

        Assert.That(app1.Equals(app2), Is.True);
        Assert.That(app1.Equals(app3), Is.False);
        Assert.That(app1.Equals(appDiffStyle), Is.False);
        Assert.That(app1.Equals(appDiffOffset), Is.False);

        var appNearOffset = new HumanoidCharacterAppearance(
            "HairBald", Color.Black, "FacialHairClean", Color.Black, Color.Brown, Color.White,
            markings, true, colors1, HairGradientStyle.Split, 0.4005f);
        Assert.That(app1.Equals(appNearOffset), Is.False);

        Assert.That(app1.GetHashCode(), Is.EqualTo(app2.GetHashCode()));
        Assert.That(app1.GetHashCode(), Is.Not.EqualTo(app3.GetHashCode()));
        Assert.That(app1.GetHashCode(), Is.Not.EqualTo(appDiffStyle.GetHashCode()));
    }

    [Test]
    public void TestWithHairGradientMethod()
    {
        var appearance = new HumanoidCharacterAppearance(
            "HairBald", Color.Black, "FacialHairClean", Color.Black, Color.Brown, Color.White,
            new List<Marking>());

        // Test padding when fewer than 2 colors provided
        var shortColors = new List<Color> { Color.Red };
        var withShort = appearance.WithHairGradient(true, shortColors);
        Assert.That(withShort.HairGradientEnabled, Is.True);
        Assert.That(withShort.HairGradientColors.Count, Is.EqualTo(2));
        Assert.That(withShort.HairGradientColors[0], Is.EqualTo(Color.Red));
        Assert.That(withShort.HairGradientColors[1], Is.EqualTo(Color.Red));

        // Test 2 colors preserved with custom style and offset
        var twoColors = new List<Color> { Color.Red, Color.Blue };
        var withTwo = appearance.WithHairGradient(true, twoColors, HairGradientStyle.Underdye, 0.7f);
        Assert.That(withTwo.HairGradientEnabled, Is.True);
        Assert.That(withTwo.HairGradientColors.Count, Is.EqualTo(2));
        Assert.That(withTwo.HairGradientColors[0], Is.EqualTo(Color.Red));
        Assert.That(withTwo.HairGradientColors[1], Is.EqualTo(Color.Blue));
        Assert.That(withTwo.HairGradientStyle, Is.EqualTo(HairGradientStyle.Underdye));
        Assert.That(withTwo.HairGradientOffset, Is.EqualTo(0.7f));

        // Test truncation when more than 2 colors provided
        var longColors = new List<Color> { Color.Red, Color.Green, Color.Blue, Color.Yellow };
        var withLong = appearance.WithHairGradient(true, longColors);
        Assert.That(withLong.HairGradientColors.Count, Is.EqualTo(2));
        Assert.That(withLong.HairGradientColors[0], Is.EqualTo(Color.Red));
        Assert.That(withLong.HairGradientColors[1], Is.EqualTo(Color.Green));

        // Test unknown styles and non-finite offsets from a client are sanitized
        var invalid = appearance.WithHairGradient(true, twoColors, (HairGradientStyle) 200, float.NaN);
        Assert.That(invalid.HairGradientStyle, Is.EqualTo(HairGradientStyle.Ombre));
        Assert.That(invalid.HairGradientOffset, Is.EqualTo(0.5f));

        var outOfRange = appearance.WithHairGradient(true, twoColors, HairGradientStyle.Split, 3f);
        Assert.That(outOfRange.HairGradientOffset, Is.EqualTo(1f));
    }

    [Test]
    public void TestPersistencePreservesPlainAndGradientHairStates()
    {
        var appearance = new HumanoidCharacterAppearance()
            .WithHairGradient(true, new[] { Color.Red, Color.Blue }, HairGradientStyle.Underdye, 0.7f);
        var humanoid = new HumanoidCharacterProfile
        {
            Species = "Human",
            Appearance = appearance,
        };

        var savedGradient = Content.Server.Database.ServerDbBase.ConvertProfiles(humanoid, 0);
        Assert.That(savedGradient.HairColor, Is.EqualTo(appearance.HairColor.ToHex()));
        Assert.That(savedGradient.HairGradientEnabled, Is.True);
        Assert.That(savedGradient.HairGradientData, Is.Not.Null);

        var plain = humanoid.WithCharacterAppearance(appearance.WithHairGradient(false, new[] { Color.Red, Color.Blue }));
        var savedPlain = Content.Server.Database.ServerDbBase.ConvertProfiles(plain, 0, savedGradient);
        Assert.That(savedPlain.HairColor, Is.EqualTo(plain.Appearance.HairColor.ToHex()));
        Assert.That(savedPlain.HairGradientEnabled, Is.False);
        Assert.That(savedPlain.HairGradientData, Is.EqualTo(savedGradient.HairGradientData));

        var loaded = Content.Server.Database.ServerDbBase.ConvertProfiles(savedPlain);
        Assert.That(loaded.Appearance.HairColor, Is.EqualTo(plain.Appearance.HairColor));
        Assert.That(loaded.Appearance.HairGradientEnabled, Is.False);
        Assert.That(loaded.Appearance.HairGradientColors, Is.EqualTo(appearance.HairGradientColors));
        Assert.That(loaded.Appearance.HairGradientStyle, Is.EqualTo(HairGradientStyle.Underdye));
        Assert.That(loaded.Appearance.HairGradientOffset, Is.EqualTo(0.7f));
    }
}

