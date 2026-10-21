using Content.Server._Arcane.Speech.EntitySystems;
using Content.Server.Speech.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Speech.Components;

[RegisterComponent, Access(typeof(EnglishAccentSystem))]
public sealed partial class EnglishAccentComponent : Component
{
    [DataField]
    public ProtoId<ReplacementAccentPrototype> Accent = "english";

    [DataField]
    public float TransliterateChance = 0.02f;

    [DataField]
    public float YeryChance = 0.75f;

    [DataField]
    public float SignChance = 0.4f;

    [DataField]
    public float ShchChance = 0.6f;

    [DataField]
    public float TseChance = 0.5f;

    [DataField]
    public float HardLChance = 0.5f;

    [DataField]
    public float LatinChance = 0.2f;

    [DataField]
    public int LatinCooldown = 2;

    [DataField]
    public float StretchChance = 0.03f;

    [DataField]
    public float PrefixChance = 0.08f;

    [DataField]
    public float SuffixChance = 0.06f;

    [DataField]
    public List<LocId> Prefixes = new()
    {
        "accent-english-prefix-1",
        "accent-english-prefix-2",
        "accent-english-prefix-3",
        "accent-english-prefix-4",
        "accent-english-prefix-5",
        "accent-english-prefix-6",
        "accent-english-prefix-7",
        "accent-english-prefix-8",
    };

    [DataField]
    public List<LocId> Suffixes = new()
    {
        "accent-english-suffix-1",
        "accent-english-suffix-2",
        "accent-english-suffix-3",
        "accent-english-suffix-4",
        "accent-english-suffix-5",
        "accent-english-suffix-6",
        "accent-english-suffix-7",
    };
}
