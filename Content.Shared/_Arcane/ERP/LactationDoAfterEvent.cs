using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ERP;

[Serializable, NetSerializable]
public sealed partial class LactationDoAfterEvent : DoAfterEvent
{
    public NetEntity Donor;
    public NetEntity Receiver;
    public ProtoId<ReagentPrototype> Reagent;
    public FixedPoint2 Amount;
    public bool Express;

    public override DoAfterEvent Clone()
    {
        return new LactationDoAfterEvent
        {
            Donor = Donor,
            Receiver = Receiver,
            Reagent = Reagent,
            Amount = Amount,
            Express = Express,
        };
    }
}
