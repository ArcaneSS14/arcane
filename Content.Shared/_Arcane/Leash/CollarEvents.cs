using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.Leash;

public sealed partial class RemoveCollarAlertEvent : BaseAlertEvent;

[Serializable, NetSerializable]
public sealed partial class RemoveCollarDoAfterEvent : SimpleDoAfterEvent
{
}
