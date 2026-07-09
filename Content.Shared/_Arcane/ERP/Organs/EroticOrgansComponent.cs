using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ERP.Organs;

[RegisterComponent]
public sealed partial class EroticOrgansComponent : Component
{
    /// <summary>
    /// Organs spawned for all sexes in the groin slot.
    /// </summary>
    [DataField]
    public List<EroticOrganEntry> GroinCommon = [];

    /// <summary>
    /// Organs spawned for Male and Futanari in the groin slot.
    /// </summary>
    [DataField]
    public List<EroticOrganEntry> GroinMale = [];

    /// <summary>
    /// Organs spawned for Female and Futanari in the groin slot.
    /// </summary>
    [DataField]
    public List<EroticOrganEntry> GroinFemale = [];

    /// <summary>
    /// Organs spawned for Female and Futanari in the chest slot.
    /// </summary>
    [DataField]
    public List<EroticOrganEntry> ChestFemale = [];

    /// <summary>
    /// Organ slots hidden when not aroused. Visual layer only shows during arousal.
    /// Used for species with retracted or cloaca-concealed genitals (e.g. xenos).
    /// </summary>
    [DataField]
    public HashSet<string> HideWhenFlaccid = [];

    /// <summary>
    /// Default visual variant per organ slot, applied when the player has no saved preference.
    /// Key = slot id (e.g. "penis"), value = variant name (e.g. "hemi").
    /// </summary>
    [DataField]
    public Dictionary<string, string> DefaultVariants = [];
}

[DataDefinition]
public sealed partial class EroticOrganEntry
{
    [DataField(required: true)]
    public EntProtoId Proto = default!;

    [DataField(required: true)]
    public string Slot = default!;
}
