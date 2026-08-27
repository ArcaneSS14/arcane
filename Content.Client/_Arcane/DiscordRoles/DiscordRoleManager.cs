using Content.Shared._Arcane.DiscordRoles;
using Robust.Client.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Client._Arcane.DiscordRoles;

public sealed class DiscordRoleManager : IPostInjectInit, ISharedDiscordRoleManager
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly HashSet<DiscordRole> _roles = [];

    public event Action? RolesUpdated;

    public bool HasRole(ICommonSession session, DiscordRole role)
    {
        return _player.LocalSession?.UserId == session.UserId && _roles.Contains(role);
    }

    private void OnStatus(DiscordRolesStatusMsg message)
    {
        _roles.Clear();
        _roles.UnionWith(message.Roles);
        RolesUpdated?.Invoke();
    }

    void IPostInjectInit.PostInject()
    {
        _net.RegisterNetMessage<DiscordRolesStatusMsg>(OnStatus);
    }
}
