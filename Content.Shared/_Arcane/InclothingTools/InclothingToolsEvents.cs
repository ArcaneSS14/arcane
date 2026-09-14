using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.InclothingTools;

public sealed partial class ActionSelectInclothingToolEvent : InstantActionEvent { }

public sealed partial class ActionRandomInclothingToolEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public sealed partial class RandomInclothingToolEvent : EntityEventArgs
{
    public NetEntity Clothing;
}

[Serializable, NetSerializable]
public enum SelectInclothingToolUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed partial class InclothingToolsUiMessage : BoundUserInterfaceMessage
{
    public NetEntity AttachedTool;

    public InclothingToolsUiMessage(NetEntity attachedTool)
    {
        AttachedTool = attachedTool;
    }
}

[Serializable, NetSerializable]
public sealed partial class InclothingToolsUnequipAllMessage : BoundUserInterfaceMessage { }