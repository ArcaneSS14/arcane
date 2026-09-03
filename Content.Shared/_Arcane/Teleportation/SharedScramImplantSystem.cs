using Robust.Shared.Audio;

namespace Content.Shared._Arcane.Teleportation;

/// <summary>
///     Shared logic for the escape scram implant action.
/// </summary>
public abstract class SharedScramImplantSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScramImplantEvent>(OnEscapeTeleport);
    }

    private void OnEscapeTeleport(ScramImplantEvent args)
    {
        args.Handled = TryEscapeTeleport(args.Performer, args.TeleportSound);
    }

    /// <summary>
    ///     Attempts to teleport the user to a random free tile on the grid they are currently on.
    /// </summary>
    /// <returns>True if the user was actually teleported, false otherwise.</returns>
    protected virtual bool TryEscapeTeleport(EntityUid user, SoundSpecifier teleportSound)
    {
        return false;
    }
}
