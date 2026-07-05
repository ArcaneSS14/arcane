using Content.Shared._Arcane.ERP;
using Content.Shared._Arcane.ERP.Organs;
using Content.Shared._Arcane.ERP.OrgansAppearance;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class EroticOrganRequirement : ErpRequirement
{
    [DataField(required: true)]
    public string Organ = string.Empty;

    [DataField]
    public bool RequireVisible = false;

    private static readonly IReadOnlyDictionary<string, Type> OrganTypes = new Dictionary<string, Type>()
    {
        ["anus"] = typeof(AnusOrganComponent),
        ["penis"] = typeof(PenisOrganComponent),
        ["testicles"] = typeof(TesticlesOrganComponent),
        ["vagina"] = typeof(VaginaOrganComponent),
        ["uterus"] = typeof(UterusOrganComponent),
        ["breasts"] = typeof(BreastsOrganComponent),
    };

    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        if (string.IsNullOrWhiteSpace(Organ))
            return false;

        if (!OrganTypes.ContainsKey(Organ))
            return false;

        if (!entityManager.TryGetComponent<BodyComponent>(uid, out var body))
            return false;

        var bodySystem = entityManager.System<SharedBodySystem>();
        foreach (var organ in bodySystem.GetBodyOrganEntityComps<EroticOrganComponent>((uid, body)))
        {
            if (!entityManager.HasComponent(organ.Owner, OrganTypes[Organ]))
                continue;

            if (RequireVisible)
            {
                // Authoritative server flag (set by EroticCoverageSystem) — always checked first.
                if (!organ.Comp1.Visible)
                    return false;

                // CoveredSlots and HideWhenFlaccid are networked for the client side.
                if (!entityManager.TryGetComponent<ErpOrganVisualsComponent>(uid, out var visuals))
                    return true;

                if (visuals.CoveredSlots.Contains(Organ))
                    return false;

                if (!visuals.HideWhenFlaccid.Contains(Organ))
                    return true;

                return entityManager.TryGetComponent<ArousalComponent>(uid, out var arousal)
                    && arousal.CurrentPhase >= ArousalPhase.Aroused;
            }
            else
                return true;
        }
        return false;
    }
}
