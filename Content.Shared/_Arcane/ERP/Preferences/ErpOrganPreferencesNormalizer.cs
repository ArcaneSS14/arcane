using Robust.Shared.Maths;

namespace Content.Shared._Arcane.ERP.Preferences;

/// <summary>
/// Server-side (and shared-testable) sanitizer for incoming ERP organ preference payloads.
/// Strips unknown slot keys, clamps sizes, whitelists variants, and sanitizes color channels.
/// </summary>
public static class ErpOrganPreferencesNormalizer
{
    public static ErpOrganPreferences Normalize(ErpOrganPreferences? input)
    {
        if (input?.Organs == null)
            return ErpOrganPreferences.Default();

        var result = new ErpOrganPreferences();
        foreach (var slotId in ErpOrganSlots.All)
        {
            if (!input.Organs.TryGetValue(slotId, out var cfg))
                continue;

            var maxSize = ErpOrganSlots.MaxSize.TryGetValue(slotId, out var ms) ? ms : 1;
            var size = Math.Clamp(cfg.Size, 1, maxSize);

            string variant;
            var allowed = ErpOrganSlots.Variants.GetValueOrDefault(slotId);
            if (allowed is { Length: > 0 })
                variant = Array.IndexOf(allowed, cfg.Variant) >= 0 ? cfg.Variant : allowed[0];
            else
                variant = cfg.Variant;

            Color? color = null;
            if (cfg.Color is { } raw)
                color = new Color(
                    ClampChannel(raw.R),
                    ClampChannel(raw.G),
                    ClampChannel(raw.B),
                    ClampChannel(raw.A, 1f));

            result.Organs[slotId] = new ErpOrganConfig
            {
                Size = size,
                Variant = variant,
                Color = color,
            };
        }
        return result;
    }

    public static ErpOrganPreferences Normalize(ErpOrganPreferences? input, IReadOnlyList<ErpOrganEditorDefinition> definitions)
    {
        if (input?.Organs == null)
            return ErpOrganPreferences.Default();

        var result = new ErpOrganPreferences();
        foreach (var def in definitions)
        {
            if (!input.Organs.TryGetValue(def.SlotId, out var cfg))
                continue;

            var size = Math.Clamp(cfg.Size, 1, def.MaxSize);

            var variant = cfg.Variant;
            if (def.Variants.Length > 0 && Array.IndexOf(def.Variants, variant) < 0)
                variant = def.DefaultVariant;

            Color? color = null;
            if (def.AllowColor && cfg.Color is { } raw)
                color = new Color(
                    ClampChannel(raw.R),
                    ClampChannel(raw.G),
                    ClampChannel(raw.B),
                    ClampChannel(raw.A, 1f));

            result.Organs[def.SlotId] = new ErpOrganConfig
            {
                Size = size,
                Variant = variant,
                Color = color,
            };
        }

        return result;
    }

    /// <summary>Clamps a single color channel, replacing non-finite values with <paramref name="fallback"/>.</summary>
    public static float ClampChannel(float v, float fallback = 0f)
        => float.IsFinite(v) ? Math.Clamp(v, 0f, 1f) : fallback;
}
