using Robust.Shared.GameStates;
using Content.Shared.Radio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

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
    /// A list of quality tools that can be used to cut this object.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> ToolQualities = new()
    {
        "Sawing"
    };

    /// <summary>
    /// The time in seconds required to cut the object.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Delay = 45.0f;

    /// <summary>
    /// The ID of the prototype radio channel for sending the notification.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<RadioChannelPrototype>)), AutoNetworkedField]
    public string RadioChannel = "Security";

    /// <summary>
    /// The localization key for the message in the communication channel.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string AlertMessage = "cuttable-item-alert-activated";
}

public sealed class CuttableCutEvent(EntityUid user, EntityUid item) : HandledEntityEventArgs
{
    public EntityUid User { get; } = user;
    public EntityUid Item { get; } = item;
}
