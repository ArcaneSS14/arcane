using Content.Shared.Access;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.Tag;

namespace Content.Shared._Arcane.Stabikor.Components;

/// <summary>
/// A component that punishes creatures with a bad tone with electric shocks
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StabikorComponent : Component
{
    /// <summary>
    ///     Stores the UID of the player who is wearing the object
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? WearingEntity;

    [ViewVariables]
    public TimeSpan LastVerbClickTime = TimeSpan.Zero;

    /// <summary>
    ///     The time in seconds for which the object is blocked
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Duration = 60f;

    /// <summary>
    ///     Timer to count down the time until withdrawal
    /// </summary>
    [AutoNetworkedField]
    public float Timer = 0f;

    /// <summary>
    ///     Is the blocking process currently active
    /// </summary>
    [AutoNetworkedField]
    public bool IsActive = false;

    /// <summary>
    ///     Electric shock damage after punishment
    /// </summary>
    [DataField]
    public int Damage = 5;

    /// <summary>
    ///     The time of the knockout after the punishment
    /// </summary>
    [DataField]
    public float TimeStan = 5.0f;

    /// <summary>
    ///     Object status: blocked or not
    /// </summary>
    [AutoNetworkedField]
    public bool IsLocked = false;

    /// <summary>
    ///     Who can CLOSE the object
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<AccessLevelPrototype>> LockAccess = new()
    {
        "Security"
    };

    /// <summary>
    ///     Who can OPEN the object
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<AccessLevelPrototype>> UnlockAccess = new()
    {
        "Armory",
        "HeadOfSecurity",
        "Captain",
        "CentralCommand"
    };

    /// <summary>
    ///     Sound of successful blocking
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier LockSound = new SoundPathSpecifier("/Audio/Effects/beep1.ogg");

    /// <summary>
    ///     The sound of a successful unlock
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier UnlockSound = new SoundPathSpecifier("/Audio/Effects/beep1.ogg");

    /// <summary>
    ///     The sound of an error / denial of access
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Effects/beep_landmine.ogg");

}

[Serializable, NetSerializable]
public sealed class OpenDialogEvent(NetEntity verp) : EntityEventArgs
{
    public NetEntity Verp = verp;
}
[Serializable, NetSerializable]
public sealed class ToggleLockEvent(NetEntity verp) : EntityEventArgs
{
    public NetEntity Verp = verp;
}
