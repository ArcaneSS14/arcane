using Content.Shared._Arcane.ERP.Organs;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ERP.Preferences;

public sealed class ErpOrganEditorDefinition
{
    public string SlotId = string.Empty;
    public string[] Variants = [];
    public string DefaultVariant = "human";
    public int MaxSize = 1;
    public bool AllowColor = true;
}

public static class ErpOrganEditorDefinitions
{
    public static IReadOnlyList<ErpOrganEditorDefinition> GetForSpecies(
        string? species,
        Sex sex,
        IPrototypeManager prototype,
        IComponentFactory componentFactory)
    {
        if (string.IsNullOrEmpty(species))
            return GetFallbackDefinitions(sex);

        if (!prototype.TryIndex<SpeciesPrototype>(species, out var speciesPrototype) ||
            !prototype.TryIndex<EntityPrototype>(speciesPrototype.Prototype, out var entityPrototype))
        {
            return GetFallbackDefinitions(sex);
        }

        if (!entityPrototype.TryGetComponent<EroticOrgansComponent>(out var organs, componentFactory))
        {
            return []; // Species exists but has no EroticOrgans — editor shows nothing
        }

        var result = new Dictionary<string, ErpOrganEditorDefinition>();
        foreach (var entry in GetEntries(organs, sex))
        {
            if (string.IsNullOrEmpty(entry.Slot) ||
                !prototype.TryIndex<EntityPrototype>(entry.Proto, out var organPrototype) ||
                !organPrototype.TryGetComponent<EroticOrganComponent>(out var organ, componentFactory) ||
                !organ.EditorVisible)
            {
                continue;
            }

            var variants = organ.EditorVariants.Count > 0
                ? organ.EditorVariants.ToArray()
                : Array.Empty<string>();

            var defaultVariant = GetDefaultVariant(organs, entry.Slot, organ, variants);
            result[entry.Slot] = new ErpOrganEditorDefinition
            {
                SlotId = entry.Slot,
                Variants = variants,
                DefaultVariant = defaultVariant,
                MaxSize = Math.Max(1, organ.EditorMaxSize),
                AllowColor = organ.EditorAllowColor,
            };
        }

        var definitions = new List<ErpOrganEditorDefinition>();
        foreach (var definition in result.Values)
            definitions.Add(definition);

        definitions.Sort((a, b) => GetSlotOrder(a.SlotId).CompareTo(GetSlotOrder(b.SlotId)));
        return definitions;
    }

    public static ErpOrganConfig CreateDefaultConfig(ErpOrganEditorDefinition def)
        => new()
        {
            Variant = def.DefaultVariant,
            Size = Math.Clamp(3, 1, def.MaxSize),
        };

    private static IEnumerable<EroticOrganEntry> GetEntries(EroticOrgansComponent organs, Sex sex)
    {
        if (sex == Sex.Unsexed)
            return organs.GroinCommon;

        var result = new List<EroticOrganEntry>();
        result.AddRange(organs.GroinCommon);

        if (sex is Sex.Male or Sex.Futanari)
            result.AddRange(organs.GroinMale);

        if (sex is Sex.Female or Sex.Futanari)
        {
            result.AddRange(organs.GroinFemale);
            result.AddRange(organs.ChestFemale);
        }

        return result;
    }

    public static string GetDefaultVariant(EroticOrgansComponent? organs, string slotId, EroticOrganComponent organ)
    {
        var variants = organ.EditorVariants.Count > 0 ? organ.EditorVariants.ToArray() : Array.Empty<string>();
        return GetDefaultVariant(organs, slotId, organ, variants);
    }

    private static string GetDefaultVariant(
        EroticOrgansComponent? organs,
        string slotId,
        EroticOrganComponent organ,
        string[] variants)
    {
        if (organs?.DefaultVariants.TryGetValue(slotId, out var speciesDefault) == true &&
            (variants.Length == 0 || Array.IndexOf(variants, speciesDefault) >= 0))
        {
            return speciesDefault;
        }

        if (variants.Length > 0 && Array.IndexOf(variants, organ.EditorDefaultVariant) < 0)
            return variants[0];

        return organ.EditorDefaultVariant;
    }

    // Fallback used when species is null/empty — respects sex so incompatible slots are not shown.
    private static IReadOnlyList<ErpOrganEditorDefinition> GetFallbackDefinitions(Sex sex)
    {
        var result = new List<ErpOrganEditorDefinition>();
        foreach (var slotId in ErpOrganSlots.EditorVisible)
        {
            if (ErpOrganSlots.SexFilter.TryGetValue(slotId, out var allowed) && Array.IndexOf(allowed, sex) < 0)
                continue;

            var variants = ErpOrganSlots.Variants.GetValueOrDefault(slotId) ?? [];
            result.Add(new ErpOrganEditorDefinition
            {
                SlotId = slotId,
                Variants = variants,
                DefaultVariant = variants.Length > 0 ? variants[0] : "human",
                MaxSize = ErpOrganSlots.MaxSize.GetValueOrDefault(slotId, 1),
            });
        }

        return result;
    }

    private static int GetSlotOrder(string slotId)
    {
        for (var i = 0; i < ErpOrganSlots.All.Count; i++)
        {
            if (ErpOrganSlots.All[i] == slotId)
                return i;
        }

        return int.MaxValue;
    }
}
