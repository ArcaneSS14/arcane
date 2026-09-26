using System.Diagnostics.CodeAnalysis;
using Content.Shared._Arcane.DiscordRoles;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.Humanoid.Markings.Effects;

/// <summary>
///     Base class for effects that gate whether a marking may be used by a given session.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class MarkingEffect
{
    /// <summary>
    ///     Tries to validate whether the marking is available to the session.
    /// </summary>
    public abstract bool Validate(
        MarkingPrototype marking,
        ICommonSession? session,
        ISharedDiscordRoleManager? discordRoles,
        [NotNullWhen(false)] out FormattedMessage? reason);
}
