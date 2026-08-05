using Robust.Shared.Player;

namespace Content.Shared._Arcane.DiscordRoles;

public interface ISharedDiscordRoleManager
{
    event Action? RolesUpdated;

    bool HasRole(ICommonSession session, DiscordRole role);
}
