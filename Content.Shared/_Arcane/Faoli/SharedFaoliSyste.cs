using Content.Shared._Arcane.Faoli.Components;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Alert;

namespace Content.Shared._Arcane.Faoli;

public sealed class SharedFaoliSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;

    public bool ChangeFaoliAmount(EntityUid uid, FixedPoint2 amount, FaoliComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        comp.Faoli += amount;
        Dirty(uid, comp);

        if (comp.Faoli >= comp.OverflowLimit)
            comp.Faoli = comp.OverflowLimit;

        if (0 >= comp.Faoli)
            comp.Faoli = 0;


        _alerts.ShowAlert(uid, comp.FaoliAlert);

        return true;
    }
}
