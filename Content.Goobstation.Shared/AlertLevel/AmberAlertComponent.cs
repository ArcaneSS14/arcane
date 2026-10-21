using Content.Shared.Access;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.AlertLevel;

/// <summary>
/// Tracks whether the amber alert level is unlocked for a station.
/// </summary>
[RegisterComponent]
public sealed partial class AmberAlertComponent : Component
{
    [DataField]
    public bool Unlocked;

    /// <summary>
    /// The first ID card (with command head access) swiped.
    /// </summary>
    [ViewVariables]
    public EntityUid? PendingCard;

    /// <summary>
    /// When the pending first authorization expires if a second swipe isn't made.
    /// </summary>
    [ViewVariables]
    public TimeSpan? PendingExpiry;

    /// <summary>
    /// How long a first authorization is held while waiting for a second command member.
    /// </summary>
    [DataField]
    public TimeSpan PendingTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The alert level this component gates.
    /// </summary>
    [DataField]
    public string AmberLevel = "amber";

    /// <summary>
    /// Any one of these access levels is required to begin the authorization (the first swipe).
    /// </summary>
    [DataField]
    public List<ProtoId<AccessLevelPrototype>> InitiatorAccess = new()
    {
        "Captain",
        "HeadOfSecurity",
    };

    /// <summary>
    /// Access required to confirm the authorization (the second swipe).
    /// </summary>
    [DataField]
    public ProtoId<AccessLevelPrototype> CommandAccess = "Command";

    /// <summary>
    /// The radio channel used to announce authorization progress and check for command comms.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> CommandChannel = "Command";

    /// <summary>
    /// The sound played when the amber alert is unlocked.
    /// </summary>
    [DataField]
    public SoundSpecifier UnlockSound = new SoundPathSpecifier("/Audio/_Goobstation/Ambience/Alert/amber_unlock_alert.ogg")
    {
        Params = AudioParams.Default
        .WithVolume(-5)
    };
}
