using System.Numerics;
using Content.Client.Stylesheets.Colorspace;

namespace Content.Client._Arcane.Humanoid;

public enum HairGradientHarmony : byte
{
    Lighten,
    Analogous,
    Contrast,
}

/// <summary>
/// Suggests gradient tips in OkLCh, the space the shader blends in, so hue steps look even.
/// </summary>
public static class HairGradientPalette
{
    private const float Achromatic = 0.03f;
    private const float Deg = MathF.PI / 180f;
    private const float MaxLightness = 0.95f;

    private const float BlondHue = 80f * Deg;

    // Share of the remaining headroom up to MaxLightness, so light roots still get distinct variants.
    private static readonly float[] LightenSteps = { 0.4f, 0.6f, 0.8f };
    private static readonly float[] AnalogousShifts = { 30f, -30f, 60f, -60f };
    private static readonly float[] ContrastShifts = { 180f, 150f, -150f, 120f, -120f };

    // Hues for gray, black and white roots, which have no hue of their own to rotate.
    private static readonly float[] AnalogousNeutralHues = { 250f, 300f, 200f, 30f };
    private static readonly float[] ContrastNeutralHues = { 220f, 340f, 150f, 50f, 280f };

    public static Color Suggest(Color roots, HairGradientHarmony harmony, int variant)
    {
        var lch = Color.ToLch(roots.LabFromSrgb());
        var (l, c, h) = (lch.X, lch.Y, lch.Z);

        return harmony switch
        {
            HairGradientHarmony.Analogous => Analogous(l, c, h, variant),
            HairGradientHarmony.Contrast => Contrast(l, c, h, variant),
            _ => Lighten(l, c, h, variant),
        };
    }

    private static Color Lighten(float l, float c, float h, int variant)
    {
        var step = Pick(LightenSteps, variant);

        // Platinum and white roots cannot get lighter, so fade them to a pale warm blond instead.
        if (l > 0.85f)
            return FromLch(0.97f - step * 0.1f, 0.02f + step * 0.05f, BlondHue);

        var targetL = l + (MaxLightness - l) * step;

        // Black and gray roots bleach into warm brown and caramel.
        if (c < Achromatic)
            return FromLch(targetL, 0.03f + step * 0.06f, 60f * Deg);

        // Natural warm hair drifts to golden blond; dyed colors keep their hue and fade to a pastel.
        var natural = h is > 20f * Deg and < 110f * Deg;
        var targetH = natural ? (float) Angle.Lerp(h, BlondHue, 0.4f).Theta : h;
        var targetC = natural ? MathF.Max(c * 0.85f, 0.05f) : c * 0.7f;
        return FromLch(targetL, targetC, targetH);
    }

    private static Color Analogous(float l, float c, float h, int variant)
    {
        var targetL = Math.Clamp(l, 0.5f, 0.85f);

        if (c < Achromatic)
            return FromLch(targetL, 0.08f, Pick(AnalogousNeutralHues, variant) * Deg);

        return FromLch(targetL, MathF.Max(c, 0.08f), h + Pick(AnalogousShifts, variant) * Deg);
    }

    private static Color Contrast(float l, float c, float h, int variant)
    {
        // Contrast in lightness as well as hue, otherwise dark tips vanish on dark roots.
        var targetL = l < 0.6f ? MathF.Max(l + 0.3f, 0.72f) : MathF.Min(l - 0.3f, 0.5f);

        if (c < Achromatic)
            return FromLch(targetL, 0.14f, Pick(ContrastNeutralHues, variant) * Deg);

        return FromLch(targetL, MathF.Max(c, 0.12f), h + Pick(ContrastShifts, variant) * Deg);
    }

    private static float Pick(float[] values, int variant)
    {
        return values[(variant % values.Length + values.Length) % values.Length];
    }

    // Lowers chroma until the color fits the sRGB gamut.
    private static Color FromLch(float l, float c, float h)
    {
        var full = ToSrgb(l, c, h);
        if (InGamut(full))
            return full;

        var low = 0f;
        var high = c;
        for (var i = 0; i < 12; i++)
        {
            var mid = (low + high) * 0.5f;
            if (InGamut(ToSrgb(l, mid, h)))
                low = mid;
            else
                high = mid;
        }

        return Color.FromLch(new Vector4(l, low, h, 1f)).LabToSrgb();
    }

    private static Color ToSrgb(float l, float c, float h)
    {
        var lab = Color.FromLch(new Vector4(l, c, h, 1f));
        return Color.ToSrgb(Color.FromLab(lab));
    }

    private static bool InGamut(Color color)
    {
        const float epsilon = 0.0005f;
        return color.R is >= -epsilon and <= 1f + epsilon &&
               color.G is >= -epsilon and <= 1f + epsilon &&
               color.B is >= -epsilon and <= 1f + epsilon;
    }
}
