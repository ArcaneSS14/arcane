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
    public void TestHairGradientDefaultState()
    {
        var appearance = new HumanoidCharacterAppearance(
            "HairBald",
            Color.Black,
            "FacialHairClean",
            Color.Black,
            Color.Brown,
            Color.White,
            new List<Marking>());

        Assert.That(appearance.HairGradientColors, Is.Not.Null);
        Assert.That(appearance.HairGradientColors.Count, Is.EqualTo(2));
        Assert.That(appearance.HairGradientEnabled, Is.False);
        Assert.That(appearance.HairGradientStyle, Is.EqualTo(HairGradientStyle.Ombre));
        Assert.That(appearance.HairGradientOffset, Is.EqualTo(0.5f));
    }

    [Test]
    public void TestHairGradientColorsPreservedThroughWithMethods()
    {
        var testColors = new List<Color>
        {
            Color.Red,
            Color.Blue,
        };

        var appearance = new HumanoidCharacterAppearance(
            "HairBald",
            Color.Black,
            "FacialHairClean",
            Color.Black,
            Color.Brown,
            Color.White,
            new List<Marking>(),
            true,
            testColors,
            HairGradientStyle.Split,
            0.75f);

        Assert.That(appearance.HairGradientEnabled, Is.True);
        Assert.That(appearance.HairGradientColors, Is.EqualTo(testColors));
        Assert.That(appearance.HairGradientStyle, Is.EqualTo(HairGradientStyle.Split));
        Assert.That(appearance.HairGradientOffset, Is.EqualTo(0.75f));

        // Test WithHairStyleName
        var modified = appearance.WithHairStyleName("HairShort");
        Assert.That(modified.HairGradientColors, Is.EqualTo(testColors));
        Assert.That(modified.HairGradientEnabled, Is.True);
        Assert.That(modified.HairGradientStyle, Is.EqualTo(HairGradientStyle.Split));
        Assert.That(modified.HairGradientOffset, Is.EqualTo(0.75f));

        // Test WithHairColor
        modified = appearance.WithHairColor(Color.Purple);
        Assert.That(modified.HairGradientColors, Is.EqualTo(testColors));
        Assert.That(modified.HairGradientEnabled, Is.True);
        Assert.That(modified.HairGradientStyle, Is.EqualTo(HairGradientStyle.Split));
        Assert.That(modified.HairGradientOffset, Is.EqualTo(0.75f));

        // Test WithFacialHairStyleName
        modified = appearance.WithFacialHairStyleName("FacialHairBeard");
        Assert.That(modified.HairGradientColors, Is.EqualTo(testColors));
        Assert.That(modified.HairGradientEnabled, Is.True);
        Assert.That(modified.HairGradientStyle, Is.EqualTo(HairGradientStyle.Split));
        Assert.That(modified.HairGradientOffset, Is.EqualTo(0.75f));

        // Test WithFacialHairColor
        modified = appearance.WithFacialHairColor(Color.White);
        Assert.That(modified.HairGradientColors, Is.EqualTo(testColors));
        Assert.That(modified.HairGradientEnabled, Is.True);
        Assert.That(modified.HairGradientStyle, Is.EqualTo(HairGradientStyle.Split));
        Assert.That(modified.HairGradientOffset, Is.EqualTo(0.75f));

        // Test WithEyeColor
        modified = appearance.WithEyeColor(Color.Cyan);
        Assert.That(modified.HairGradientColors, Is.EqualTo(testColors));
        Assert.That(modified.HairGradientEnabled, Is.True);
        Assert.That(modified.HairGradientStyle, Is.EqualTo(HairGradientStyle.Split));
        Assert.That(modified.HairGradientOffset, Is.EqualTo(0.75f));

        // Test WithSkinColor
        modified = appearance.WithSkinColor(Color.Pink);
        Assert.That(modified.HairGradientColors, Is.EqualTo(testColors));
        Assert.That(modified.HairGradientEnabled, Is.True);
        Assert.That(modified.HairGradientStyle, Is.EqualTo(HairGradientStyle.Split));
        Assert.That(modified.HairGradientOffset, Is.EqualTo(0.75f));

        // Test WithMarkings
        var markingList = new List<Marking> { new("TestMarking", new List<Color> { Color.Red }) };
        modified = appearance.WithMarkings(markingList);
        Assert.That(modified.HairGradientColors, Is.EqualTo(testColors));
        Assert.That(modified.HairGradientEnabled, Is.True);
        Assert.That(modified.HairGradientStyle, Is.EqualTo(HairGradientStyle.Split));
        Assert.That(modified.HairGradientOffset, Is.EqualTo(0.75f));
    }

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
    }

    [Test]
    public void TestJsonSerializationRoundTrip()
    {
        var original = new Content.Server.Database.ServerDbBase.HairGradientSaveData(new List<string> { "#FF0000", "#0000FF" }, HairGradientStyle.Underdye, 0.7f);
        var json = System.Text.Json.JsonSerializer.Serialize(original, Content.Server.Database.ServerDbBase.HairGradientJsonOptions);
        TestContext.Out.WriteLine($"Serialized JSON: {json}");
        Assert.That(json, Does.Contain("Underdye"));
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Content.Server.Database.ServerDbBase.HairGradientSaveData>(json, Content.Server.Database.ServerDbBase.HairGradientJsonOptions);
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.Style, Is.EqualTo(HairGradientStyle.Underdye));
        Assert.That(deserialized.Offset, Is.EqualTo(0.7f));
        Assert.That(deserialized.Colors, Is.EqualTo(original.Colors));
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

