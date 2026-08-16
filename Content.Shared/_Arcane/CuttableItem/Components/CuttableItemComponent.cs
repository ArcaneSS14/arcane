using Robust.Shared.GameStates;
using Content.Shared.Radio;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.CuttableItem.Components;

/// <summary>
/// This component allows you to get rid of an object using tools, but notifies others about it via a radio channel.
/// </summary>
[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class CuttableItemComponent : Component
{
    /// <summary>
    /// The quality of the tools needed to cut the object.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public List<ProtoId<ToolQualityPrototype>> ToolQualities = new();

    /// <summary>
    /// The time in seconds required to cut through the object.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Delay = 45.0f;

    /// <summary>
    /// The ID of the prototype radio channel for sending notifications when an attempt is made to cut through.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "Security";

    /// <summary>
    /// The localization key for the message being sent to the communication channel.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId AlertMessage = "cuttable-item-alert-activated";
}

public sealed class CuttableCutEvent(EntityUid user) : HandledEntityEventArgs
{
    public EntityUid User { get; } = user;
}
