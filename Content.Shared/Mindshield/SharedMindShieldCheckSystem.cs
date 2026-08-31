using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;

namespace Content.Shared.Mindshield;

/// <summary>
/// Shared system that checks whether a MindShield implant blocks syndicate actions.
/// Exceptions: traitors and nuclear operatives are not blocked.
/// </summary>
public sealed class SharedMindShieldCheckSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _role = default!;

    /// <summary>
    /// Returns true if the user has a MindShield implant and does not have
    /// an exception role (traitor, nukeop).
    /// </summary>
    public bool IsMindShieldBlocked(EntityUid user)
    {
        if (!HasComp<MindShieldComponent>(user))
            return false;

        if (_mind.TryGetMind(user, out var mindId, out _))
        {
            if (_role.MindHasRole<TraitorRoleComponent>(mindId)
                || _role.MindHasRole<NukeopsRoleComponent>(mindId)
                || _role.MindHasRole<WizardRoleComponent>(mindId))
                return false;
        }

        return true;
    }
}
