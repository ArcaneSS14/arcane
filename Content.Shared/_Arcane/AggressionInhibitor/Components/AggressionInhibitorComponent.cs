using Content.Shared.Access;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Arcane.AggressionInhibitor.Components;

/// <summary>
/// A component that punishes creatures with a bad tone with electric shocks
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AggressionInhibitorComponent : Component
{
    /// <summary>
    ///     Stores the UID of the player who is wearing the object
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? WearingEntity;

    [ViewVariables]
    public TimeSpan LastVerbClickTime = TimeSpan.Zero;

    /// <summary>
    /// When to go to the next step of the schedule.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan NextUpdate;

    /// <summary>
    ///     The time in seconds for which the object is blocked
    /// </summary>
    [AutoNetworkedField]
    public float Duration = 60f;

    /// <summary>
    ///     Is the blocking process currently active
    /// </summary>
    [AutoNetworkedField]
    public bool IsActive = false;

    /// <summary>
    ///     Electric shock damage after punishment
    /// </summary>
    [DataField]
    public int Damage = 10;

    /// <summary>
    /// The time of the knockout after the punishment (in seconds).
    /// </summary>
    [DataField]
    public float TimeStun = 10.0f;


    /// <summary>
    ///     Object status: blocked or not
    /// </summary>
    [AutoNetworkedField]
    public bool IsLocked = false;

    /// <summary>
    ///     Who can CLOSE the object
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public List<ProtoId<AccessLevelPrototype>> LockAccess = new();

    /// <summary>
    ///     Who can OPEN the object
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public List<ProtoId<AccessLevelPrototype>> UnlockAccess = new();

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

/// <summary>
/// A local event for requesting the opening of the time dialog
/// </summary>
public sealed class OpenDialogEvent(EntityUid target, EntityUid user) : EntityEventArgs
{
    public EntityUid Target { get; } = target;
    public EntityUid User { get; } = user;
}

/// <summary>
/// A local event for requesting a lock change (ToggleLock)
/// </summary>
public sealed class ToggleLockEvent(EntityUid target, EntityUid user) : EntityEventArgs
{
    public EntityUid Target { get; } = target;
    public EntityUid User { get; } = user;
}
