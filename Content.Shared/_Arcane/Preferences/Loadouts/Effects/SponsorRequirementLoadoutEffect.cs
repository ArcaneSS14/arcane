using System.Diagnostics.CodeAnalysis;
using Content.Shared._Arcane.Sponsor;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Preferences.Loadouts.Effects;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.Preferences.Loadouts.Effects;

/// <summary>
///     Проверяет наличие определённого тира у пользователя.
/// </summary>
public sealed partial class SponsorRequirementLoadoutEffect : LoadoutEffect
{
    [DataField(required: true)]
    public HashSet<string> Tiers;

    public override bool Validate(HumanoidCharacterProfile profile, RoleLoadout loadout, ICommonSession? session, IDependencyCollection collection, [NotNullWhen(false)] out FormattedMessage? reason)
    {
        if (session == null)
        {
            reason = FormattedMessage.Empty;
            return false;
        }

        var sponsor = collection.Resolve<ISharedSponsorManager>();

        var isSponsor = false;
        foreach (var tier in Tiers)
            isSponsor = sponsor.HasSponsor(session, tier) ? true : isSponsor;

        reason = FormattedMessage.FromUnformatted(Loc.GetString("loadout-sponsor-requirement"));
        return isSponsor;
    }
}
