using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Arcane.DiscordRoles;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.Humanoid.Markings.Effects;

/// <summary>
///     Verifies possession of a specific Discord role that unlocks the sponsor marking
/// </summary>
public sealed partial class SponsorRequirementMarkingEffect : MarkingEffect
{
    [DataField(required: true)]
    public HashSet<DiscordRole> Roles;

    public override bool Validate(
        MarkingPrototype marking,
        ICommonSession? session,
        ISharedDiscordRoleManager? discordRoles,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = FormattedMessage.FromUnformatted(Loc.GetString("marking-sponsor-requirement"));

        if (session == null || discordRoles == null)
            return true;

        return Roles.Any(role => discordRoles.HasRole(session, role));
    }
}
