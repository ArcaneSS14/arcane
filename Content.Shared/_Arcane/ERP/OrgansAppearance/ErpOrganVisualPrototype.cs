using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ERP.OrgansAppearance;

public enum ErpStateMode : byte
{
    /// <summary>State name = cfg.Variant, with optional alias remapping via VariantAliases.</summary>
    Variant,
    /// <summary>State name = SizeStates[clamp(cfg.Size - 1, 0, len - 1)].</summary>
    SizeIndexed,
    /// <summary>State name = FlaccidState when phase below Aroused, otherwise ArousalStates[phase].</summary>
    Arousal,
    /// <summary>State name is always FixedState.</summary>
    Fixed,
    /// <summary>State name = cfg.Size.ToString() for numbered RSI states like "1", "2", …</summary>
    SizeString,
    /// <summary>FlaccidState when phase below Aroused, otherwise SizeStates[clamp(cfg.Size - 1, 0, len - 1)].</summary>
    SizeIndexedArousal,
}

/// <summary>
/// Maps a slot + species combination to an RSI and a state-selection rule.
/// The client organ visuals system builds per-species lookup tables from all loaded instances.
/// Species list is non-exclusive: one entity species must appear in exactly one prototype per slot.
/// An entry with an empty Species list acts as the slot fallback for any unmatched species.
/// </summary>
[Prototype("erpOrganVisual")]
public sealed class ErpOrganVisualPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>ErpOrganSlots constant this entry targets, e.g. "penis".</summary>
    [DataField(required: true)]
    public string Slot = string.Empty;

    /// <summary>
    /// Species prototype IDs this entry applies to.
    /// Leave empty to use this entry as the slot-level fallback.
    /// </summary>
    [DataField]
    public List<string> Species = [];

    /// <summary>RSI resource path, e.g. "/Textures/_Arcane/ERP/Mobs/Breasts/human.rsi".</summary>
    [DataField(required: true)]
    public string Rsi = string.Empty;

    [DataField]
    public ErpStateMode StateMode = ErpStateMode.Variant;

    /// <summary>Lower values render below higher values for organ layers on the humanoid sprite.</summary>
    [DataField]
    public int DrawOrder;

    /// <summary>Ordered state names for SizeIndexed mode. Index = cfg.Size - 1, clamped.</summary>
    [DataField]
    public List<string> SizeStates = [];

    /// <summary>State used when arousal phase is below Aroused in Arousal mode.</summary>
    [DataField]
    public string FlaccidState = "flaccid";

    /// <summary>
    /// Maps ArousalPhase name → RSI state for Arousal mode.
    /// Phases not listed fall through to FlaccidState.
    /// </summary>
    [DataField]
    public Dictionary<string, string> ArousalStates = [];

    /// <summary>Remaps cfg.Variant values in Variant mode, e.g. "human" → "donut".</summary>
    [DataField]
    public Dictionary<string, string> VariantAliases = [];

    /// <summary>Always-used state name in Fixed mode.</summary>
    [DataField]
    public string FixedState = string.Empty;

    /// <summary>Sprite layer map key derived from slot: "erp_{slot}".</summary>
    public string LayerKey => $"erp_{Slot}";
}
