using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.WashingMachine.Events;

[Serializable, NetSerializable]
public sealed partial class StuffInWashingMachineDoAfterEvent : SimpleDoAfterEvent
{
}

