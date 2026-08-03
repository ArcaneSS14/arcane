using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Arcane.DiscordRoles;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Preferences.Loadouts.Effects;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.Preferences.Loadouts.Effects;

/// <summary>
///     Проверяет наличие одной из Discord-ролей, открывающих спонсорский лодаут.
/// </summary>
public sealed partial class SponsorRequirementLoadoutEffect : LoadoutEffect
{
    [DataField(required: true)]
    public HashSet<DiscordRole> Roles;

    public override bool Validate(HumanoidCharacterProfile profile, RoleLoadout loadout, ICommonSession? session, IDependencyCollection collection, [NotNullWhen(false)] out FormattedMessage? reason)
    {
        if (session == null)
        {
            reason = FormattedMessage.Empty;
            return false;
        }

        var discordRoles = collection.Resolve<ISharedDiscordRoleManager>();
        var hasRole = Roles.Any(role => discordRoles.HasRole(session, role));

        reason = FormattedMessage.FromUnformatted(Loc.GetString("loadout-sponsor-requirement"));
        return hasRole;
    }
}
