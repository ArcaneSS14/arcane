using System.Diagnostics.CodeAnalysis;
using Content.Shared._Arcane.DiscordRoles;
using Content.Shared._Orion.CustomGhost;
using Robust.Shared.Player;

namespace Content.Shared._Arcane.Ghost;

[DataDefinition]
public sealed partial class SponsorGhostRestriction : CustomGhostRestriction
{
    [DataField]
    public DiscordRole Role = DiscordRole.SponsorTier2;

    public override bool CanUse(ICommonSession player, [NotNullWhen(false)] out string? failReason)
    {
        if (IoCManager.Resolve<ISharedDiscordRoleManager>().HasRole(player, Role))
        {
            failReason = null;
            return true;
        }

        failReason = Loc.GetString("custom-ghost-fail-sponsor-tier-two");
        return false;
    }
}
