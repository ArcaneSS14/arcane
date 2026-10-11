using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.WashingMachine.Events;

[Serializable, NetSerializable]
public sealed partial class StuffInWashingMachineDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates? Coordinates;

    [NonSerialized]
    public EntityCoordinates? TargetCoordinates;

    public override DoAfterEvent Clone()
    {
        return new StuffInWashingMachineDoAfterEvent
        {
            Coordinates = Coordinates,
            TargetCoordinates = TargetCoordinates,
        };
    }
}
