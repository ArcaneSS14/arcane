using System.Linq;

namespace Content.Shared._Arcane.DiscordRoles;

/// <summary>
///     Maps game-facing Discord roles to Discord snowflake IDs.
/// </summary>
public sealed class DiscordRoleIdMap
{
    private readonly Dictionary<DiscordRole, ulong> _roleIds = [];

    public IReadOnlyDictionary<DiscordRole, ulong> RoleIds => _roleIds;

    public DiscordRoleIdMap()
    {
    }

    public DiscordRoleIdMap(IEnumerable<KeyValuePair<DiscordRole, ulong>> roleIds)
    {
        Replace(roleIds);
    }

    public void Set(DiscordRole role, ulong roleId)
    {
        if (roleId == 0)
            throw new ArgumentOutOfRangeException(nameof(roleId), "Discord role IDs cannot be zero.");

        if (_roleIds.Any(pair => pair.Key != role && pair.Value == roleId))
            throw new InvalidOperationException($"Discord role ID {roleId} is already assigned to another role.");

        _roleIds[role] = roleId;
    }

    public void Replace(IEnumerable<KeyValuePair<DiscordRole, ulong>> roleIds)
    {
        var replacement = new DiscordRoleIdMap();
        foreach (var (role, roleId) in roleIds)
            replacement.Set(role, roleId);

        _roleIds.Clear();
        foreach (var (role, roleId) in replacement.RoleIds)
            _roleIds.Add(role, roleId);
    }

    public bool TryGetId(DiscordRole role, out ulong roleId)
    {
        return _roleIds.TryGetValue(role, out roleId);
    }

    public HashSet<DiscordRole> Resolve(IEnumerable<ulong> roleIds)
    {
        var ids = roleIds.ToHashSet();
        var roles = new HashSet<DiscordRole>();

        foreach (var (role, roleId) in _roleIds)
        {
            if (ids.Contains(roleId))
                roles.Add(role);
        }

        return roles;
    }
}
