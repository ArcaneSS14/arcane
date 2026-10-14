using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel;

[Serializable, NetSerializable]
public enum ErpPanelKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ErpPanelBuiState(NetEntity user, NetEntity target) : BoundUserInterfaceState
{
    public NetEntity User { get; } = user;
    public NetEntity Target { get; } = target;
}

[Serializable, NetSerializable]
public sealed partial class ErpPanelSendMessage(string interaction) : BoundUserInterfaceMessage
{
    public readonly string Interaction = interaction;
}

[Serializable, NetSerializable]
public sealed partial class ErpPanelSetMultipliersMessage(float arousal, float moaning) : BoundUserInterfaceMessage
{
    public readonly float Arousal = arousal;
    public readonly float Moaning = moaning;
}

[ByRefEvent]
public readonly record struct ErpPanelOpenEvent(EntityUid Target);
