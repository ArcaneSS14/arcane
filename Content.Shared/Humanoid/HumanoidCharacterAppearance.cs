// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared.Humanoid;

// Arcane-Start
[Serializable, NetSerializable]
public enum HairGradientStyle : byte
{
    Ombre = 0,
    Split = 1,
    Underdye = 2,
}
// Arcane-End

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class HumanoidCharacterAppearance : ICharacterAppearance, IEquatable<HumanoidCharacterAppearance>
{
    [DataField("hair")]
    public string HairStyleId { get; set; } = HairStyles.DefaultHairStyle;

    [DataField]
    public Color HairColor { get; set; } = Color.Black;

    // Arcane-Start
    [DataField]
    public bool HairGradientEnabled { get; set; }

    [DataField]
    public List<Color> HairGradientColors { get; set; } = new() { Color.Black, Color.Black };

    [DataField]
    public HairGradientStyle HairGradientStyle { get; set; } = HairGradientStyle.Ombre;

    [DataField]
    public float HairGradientOffset { get; set; } = 0.5f;
    // Arcane-End

    [DataField("facialHair")]
    public string FacialHairStyleId { get; set; } = HairStyles.DefaultFacialHairStyle;

    [DataField]
    public Color FacialHairColor { get; set; } = Color.Black;

    [DataField]
    public Color EyeColor { get; set; } = Color.Black;

    [DataField]
    public Color SkinColor { get; set; } = Color.FromHsv(new Vector4(0.07f, 0.2f, 1f, 1f));

    [DataField]
    public List<Marking> Markings { get; set; } = new();

    // Arcane-Start
    [DataField]
    public bool EarsAboveHair;
    // Arcane-End

    public HumanoidCharacterAppearance(string hairStyleId,
        Color hairColor,
        string facialHairStyleId,
        Color facialHairColor,
        Color eyeColor,
        Color skinColor,
        List<Marking> markings,
        // Arcane-Start
        bool hairGradientEnabled = false,
        IReadOnlyList<Color>? hairGradientColors = null,
        HairGradientStyle hairGradientStyle = HairGradientStyle.Ombre,
        float hairGradientOffset = 0.5f
        bool earsAboveHair = false)
        // Arcane-End
    {
        HairStyleId = hairStyleId;
        HairColor = ClampColor(hairColor);
        FacialHairStyleId = facialHairStyleId;
        FacialHairColor = ClampColor(facialHairColor);
        EyeColor = ClampColor(eyeColor);
        SkinColor = ClampColor(skinColor);
        Markings = markings;
        EarsAboveHair = earsAboveHair;
    }

    public HumanoidCharacterAppearance(HumanoidCharacterAppearance other) :
        this(other.HairStyleId, other.HairColor, other.FacialHairStyleId, other.FacialHairColor, other.EyeColor, other.SkinColor, new(other.Markings), other.EarsAboveHair) // Arcane-Edit
        // Arcane-Start
        HairGradientEnabled = hairGradientEnabled;
        HairGradientStyle = hairGradientStyle;
        HairGradientOffset = Math.Clamp(hairGradientOffset, 0f, 1f);
        if (hairGradientColors != null && hairGradientColors.Count > 0)
        {
            var colors = hairGradientColors.Take(2).Select(ClampColor).ToList();
            while (colors.Count < 2)
                colors.Add(colors.Count == 0 ? HairColor : colors[^1]);
            HairGradientColors = colors;
        }
        else
        {
            HairGradientColors = new() { Color.Black, Color.Black };
        }
        // Arcane-End
    }

    public HumanoidCharacterAppearance(HumanoidCharacterAppearance other) :
        this(other.HairStyleId, other.HairColor, other.FacialHairStyleId, other.FacialHairColor, other.EyeColor, other.SkinColor, new(other.Markings), other.HairGradientEnabled, other.HairGradientColors, other.HairGradientStyle, other.HairGradientOffset) // Arcane-Edit
    {
    }

    public HumanoidCharacterAppearance WithHairStyleName(string newName)
    {
        return new(newName, HairColor, FacialHairStyleId, FacialHairColor, EyeColor, SkinColor, Markings, HairGradientEnabled, HairGradientColors, HairGradientStyle, HairGradientOffset, EarsAboveHair); // Arcane-Edit
    }

    public HumanoidCharacterAppearance WithHairColor(Color newColor)
    {
        return new(HairStyleId, newColor, FacialHairStyleId, FacialHairColor, EyeColor, SkinColor, Markings, HairGradientEnabled, HairGradientColors, HairGradientStyle, HairGradientOffset, EarsAboveHair); // Arcane-Edit
    }

    public HumanoidCharacterAppearance WithFacialHairStyleName(string newName)
    {
        return new(HairStyleId, HairColor, newName, FacialHairColor, EyeColor, SkinColor, Markings, HairGradientEnabled, HairGradientColors, HairGradientStyle, HairGradientOffset, EarsAboveHair); // Arcane-Edit
    }

    public HumanoidCharacterAppearance WithFacialHairColor(Color newColor)
    {
        return new(HairStyleId, HairColor, FacialHairStyleId, newColor, EyeColor, SkinColor, Markings, HairGradientEnabled, HairGradientColors, HairGradientStyle, HairGradientOffset, EarsAboveHair); // Arcane-Edit
    }

    public HumanoidCharacterAppearance WithEyeColor(Color newColor)
    {
        return new(HairStyleId, HairColor, FacialHairStyleId, FacialHairColor, newColor, SkinColor, Markings, HairGradientEnabled, HairGradientColors, HairGradientStyle, HairGradientOffset, EarsAboveHair); // Arcane-Edit
    }

    public HumanoidCharacterAppearance WithSkinColor(Color newColor)
    {
        return new(HairStyleId, HairColor, FacialHairStyleId, FacialHairColor, EyeColor, newColor, Markings, HairGradientEnabled, HairGradientColors, HairGradientStyle, HairGradientOffset, EarsAboveHair); // Arcane-Edit
    }

    public HumanoidCharacterAppearance WithMarkings(List<Marking> newMarkings)
    {
        return new(HairStyleId, HairColor, FacialHairStyleId, FacialHairColor, EyeColor, SkinColor, newMarkings, HairGradientEnabled, HairGradientColors, HairGradientStyle, HairGradientOffset, EarsAboveHair); // Arcane-Edit
    }

    // Arcane-Start
    public HumanoidCharacterAppearance WithEarsAboveHair(bool newValue)
    {
        return new(HairStyleId, HairColor, FacialHairStyleId, FacialHairColor, EyeColor, SkinColor, Markings, newValue, HairGradientEnabled, HairGradientColors, HairGradientStyle, HairGradientOffset, EarsAboveHair); 
    }

    public HumanoidCharacterAppearance WithHairGradient(bool enabled, IReadOnlyList<Color> colors, HairGradientStyle? style = null, float? offset = null)
    {
        var gradientColors = colors.Take(2).Select(ClampColor).ToList();
        while (gradientColors.Count < 2)
            gradientColors.Add(gradientColors.Count == 0 ? HairColor : gradientColors[^1]);
        var appearance = new HumanoidCharacterAppearance(this)
        {
            HairGradientEnabled = enabled,
            HairGradientColors = gradientColors,
            HairGradientStyle = style ?? HairGradientStyle,
            HairGradientOffset = Math.Clamp(offset ?? HairGradientOffset, 0f, 1f),
        };
        return appearance;
    }
    // Arcane-End

    public static HumanoidCharacterAppearance DefaultWithSpecies(string species)
    {
        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        var speciesPrototype = protoMan.Index<SpeciesPrototype>(species);
        var skinColoration = protoMan.Index(speciesPrototype.SkinColoration).Strategy;
        var skinColor = skinColoration.InputType switch
        {
            SkinColorationStrategyInput.Unary => skinColoration.FromUnary(speciesPrototype.DefaultHumanSkinTone),
            SkinColorationStrategyInput.Color => skinColoration.ClosestSkinColor(speciesPrototype.DefaultSkinTone),
            _ => skinColoration.ClosestSkinColor(speciesPrototype.DefaultSkinTone),
        };

        return new(
            HairStyles.DefaultHairStyle,
            Color.Black,
            HairStyles.DefaultFacialHairStyle,
            Color.Black,
            Color.Black,
            skinColor,
            new()
        );
    }

    private static IReadOnlyList<Color> _realisticEyeColors = new List<Color>
    {
        Color.Brown,
        Color.Gray,
        Color.Azure,
        Color.SteelBlue,
        Color.Black
    };

    public static HumanoidCharacterAppearance Random(string species, Sex sex)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var markingManager = IoCManager.Resolve<MarkingManager>();
        var hairStyles = markingManager.MarkingsByCategoryAndSpecies(MarkingCategories.Hair, species).Keys.ToList();
        var facialHairStyles = markingManager.MarkingsByCategoryAndSpecies(MarkingCategories.FacialHair, species).Keys.ToList();

        var newHairStyle = hairStyles.Count > 0
            ? random.Pick(hairStyles)
            : HairStyles.DefaultHairStyle.Id;

        var newFacialHairStyle = facialHairStyles.Count == 0 || sex == Sex.Female
            ? HairStyles.DefaultFacialHairStyle.Id
            : random.Pick(facialHairStyles);

        var newHairColor = random.Pick(HairStyles.RealisticHairColors);
        newHairColor = newHairColor
            .WithRed(RandomizeColor(newHairColor.R))
            .WithGreen(RandomizeColor(newHairColor.G))
            .WithBlue(RandomizeColor(newHairColor.B));

        // TODO: Add random markings

        var newEyeColor = random.Pick(_realisticEyeColors);

        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        var skinType = protoMan.Index<SpeciesPrototype>(species).SkinColoration;
        var strategy = protoMan.Index(skinType).Strategy;

        var newSkinColor = strategy.InputType switch
        {
            SkinColorationStrategyInput.Unary => strategy.FromUnary(random.NextFloat(0f, 100f)),
            SkinColorationStrategyInput.Color => strategy.ClosestSkinColor(new Color(random.NextFloat(1), random.NextFloat(1), random.NextFloat(1), 1)),
            _ => strategy.ClosestSkinColor(new Color(random.NextFloat(1), random.NextFloat(1), random.NextFloat(1), 1)),
        };

        return new HumanoidCharacterAppearance(newHairStyle, newHairColor, newFacialHairStyle, newHairColor, newEyeColor, newSkinColor, new());

        float RandomizeColor(float channel)
        {
            return MathHelper.Clamp01(channel + random.Next(-25, 25) / 100f);
        }
    }

    public static Color ClampColor(Color color)
    {
        return new(color.RByte, color.GByte, color.BByte);
    }

    public static HumanoidCharacterAppearance EnsureValid(HumanoidCharacterAppearance appearance, string species, Sex sex)
    {
        var hairStyleId = appearance.HairStyleId;
        var facialHairStyleId = appearance.FacialHairStyleId;

        var hairColor = ClampColor(appearance.HairColor);
        var facialHairColor = ClampColor(appearance.FacialHairColor);
        var eyeColor = ClampColor(appearance.EyeColor);

        var proto = IoCManager.Resolve<IPrototypeManager>();
        var markingManager = IoCManager.Resolve<MarkingManager>();

        if (!markingManager.MarkingsByCategory(MarkingCategories.Hair).ContainsKey(hairStyleId))
        {
            hairStyleId = HairStyles.DefaultHairStyle;
        }

        if (!markingManager.MarkingsByCategory(MarkingCategories.FacialHair).ContainsKey(facialHairStyleId))
        {
            facialHairStyleId = HairStyles.DefaultFacialHairStyle;
        }

        var markingSet = new MarkingSet();
        var skinColor = appearance.SkinColor;
        if (proto.TryIndex(species, out SpeciesPrototype? speciesProto))
        {
            markingSet = new MarkingSet(appearance.Markings, speciesProto.MarkingPoints, markingManager, proto);
            markingSet.EnsureValid(markingManager);

            var strategy = proto.Index(speciesProto.SkinColoration).Strategy;
            skinColor = strategy.EnsureVerified(skinColor);

            markingSet.EnsureSpecies(species, skinColor, markingManager);
            markingSet.EnsureSexes(sex, markingManager);
        }

        return new HumanoidCharacterAppearance(
            hairStyleId,
            hairColor,
            facialHairStyleId,
            facialHairColor,
            eyeColor,
            skinColor,
            markingSet.GetForwardEnumerator().ToList(),
            // Arcane-Start
            appearance.HairGradientEnabled,
            appearance.HairGradientColors,
            appearance.HairGradientStyle,
            appearance.HairGradientOffset,
            appearance.EarsAboveHair);
            // Arcane-End
    }

    public bool MemberwiseEquals(ICharacterAppearance maybeOther)
    {
        if (maybeOther is not HumanoidCharacterAppearance other) return false;
        if (HairStyleId != other.HairStyleId) return false;
        if (!HairColor.Equals(other.HairColor)) return false;
        // Arcane-Start
        if (HairGradientEnabled != other.HairGradientEnabled) return false;
        if (!HairGradientColors.SequenceEqual(other.HairGradientColors)) return false;
        if (HairGradientStyle != other.HairGradientStyle) return false;
        if (Math.Abs(HairGradientOffset - other.HairGradientOffset) > 0.001f) return false;
        // Arcane-End
        if (FacialHairStyleId != other.FacialHairStyleId) return false;
        if (!FacialHairColor.Equals(other.FacialHairColor)) return false;
        if (!EyeColor.Equals(other.EyeColor)) return false;
        if (!SkinColor.Equals(other.SkinColor)) return false;
        if (!Markings.SequenceEqual(other.Markings)) return false;
        if (EarsAboveHair != other.EarsAboveHair) return false; // Arcane
        return true;
    }

    public bool Equals(HumanoidCharacterAppearance? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return HairStyleId == other.HairStyleId &&
               HairColor.Equals(other.HairColor) &&
               // Arcane-Start
               HairGradientEnabled == other.HairGradientEnabled &&
               HairGradientColors.SequenceEqual(other.HairGradientColors) &&
               HairGradientStyle == other.HairGradientStyle &&
               HairGradientOffset.Equals(other.HairGradientOffset) &&
               // Arcane-End
               FacialHairStyleId == other.FacialHairStyleId &&
               FacialHairColor.Equals(other.FacialHairColor) &&
               EyeColor.Equals(other.EyeColor) &&
               SkinColor.Equals(other.SkinColor) &&
               Markings.SequenceEqual(other.Markings) &&
               EarsAboveHair == other.EarsAboveHair; // Arcane
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is HumanoidCharacterAppearance other && Equals(other);
    }

    // Arcane-Edit-Start
    public override int GetHashCode()
    {
        var hash = HashCode.Combine(
            HashCode.Combine(HairStyleId, HairColor, HairGradientEnabled),
            FacialHairStyleId, FacialHairColor, EyeColor, SkinColor, Markings, EarsAboveHair);
        hash = HashCode.Combine(hash, HairGradientStyle, HairGradientOffset);
        foreach (var color in HairGradientColors)
        {
            hash = HashCode.Combine(hash, color);
        }
        return hash;
    }
    // Arcane-Edit-End

    public HumanoidCharacterAppearance Clone()
    {
        return new(this);
    }
}
