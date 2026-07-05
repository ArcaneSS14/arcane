using Content.Shared.Humanoid;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ERP.Preferences;

/// <summary>
/// Per-character organ appearance configuration. Stored separately from HumanoidCharacterProfile.
/// </summary>
[Serializable, NetSerializable]
public sealed class ErpOrganPreferences
{
    /// <summary>Organ configs keyed by organ slot id ("penis", "vagina", "breasts", "testicles", "anus").</summary>
    public Dictionary<string, ErpOrganConfig> Organs { get; set; } = new();

    public ErpOrganConfig GetOrgan(string slotId)
        => Organs.TryGetValue(slotId, out var cfg) ? cfg : new ErpOrganConfig();

    public void SetOrgan(string slotId, ErpOrganConfig cfg)
        => Organs[slotId] = cfg;

    public static ErpOrganPreferences Default() => new();

    public ErpOrganPreferences Clone()
    {
        var copy = new ErpOrganPreferences();
        foreach (var (slot, cfg) in Organs)
            copy.Organs[slot] = new ErpOrganConfig { Variant = cfg.Variant, Size = cfg.Size, Color = cfg.Color };
        return copy;
    }
}

[Serializable, NetSerializable]
public sealed class ErpOrganConfig
{
    /// <summary>Visual variant from RSI, e.g. "human", "knotted", "equine".</summary>
    public string Variant { get; set; } = "human";

    /// <summary>Size index 1–8.</summary>
    public int Size { get; set; } = 3;

    /// <summary>Tint color. Null = use character skin color.</summary>
    public Color? Color { get; set; } = null;
}

/// <summary>Organ slot ids used as keys in ErpOrganPreferences.</summary>
public static class ErpOrganSlots
{
    public const string Penis = "penis";
    public const string Vagina = "vagina";
    public const string Breasts = "breasts";
    public const string Testicles = "testicles";
    public const string Anus = "anus";
    public const string Butt = "butt";

    public static readonly IReadOnlyList<string> All = [Vagina, Breasts, Testicles, Penis, Butt, Anus];

    public static readonly IReadOnlyList<string> EditorVisible = [Vagina, Breasts, Penis];

    /// <summary>Slots not listed here are visible for all sexes.</summary>
    public static readonly IReadOnlyDictionary<string, Sex[]> SexFilter =
        new Dictionary<string, Sex[]>
        {
            [Penis]     = [Sex.Male, Sex.Futanari],
            [Testicles] = [Sex.Male, Sex.Futanari],
            [Vagina]    = [Sex.Female, Sex.Futanari],
            [Breasts]   = [Sex.Female, Sex.Futanari],
        };

    /// <summary>
    /// Fallback variant list per slot, used only when the species entity cannot be resolved to an EroticOrgans
    /// component. The canonical per-species variant set lives in the organ entity prototypes.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> Variants =
        new Dictionary<string, string[]>
        {
            [Vagina]    = ["human", "gaping", "hairy", "spade"],
            [Testicles] = ["single", "sheath", "hidden"],
            [Anus]      = ["donut", "squished"],
        };

    /// <summary>Maximum size index per slot (slider range 1..N). Slots not listed have no size control.</summary>
    public static readonly IReadOnlyDictionary<string, int> MaxSize =
        new Dictionary<string, int>
        {
            [Penis]     = 5,
            [Breasts]   = 4,
            [Testicles] = 5,
            [Butt]      = 5,
            [Anus]      = 8,
        };
}
