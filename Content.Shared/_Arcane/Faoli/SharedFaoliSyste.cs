using Content.Shared._Arcane.Faoli.Components;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Alert;
using Content.Shared.Popups;

namespace Content.Shared._Arcane.Faoli;

public sealed class SharedFaoliSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public bool TryChangeFaoliAmount(EntityUid uid, FixedPoint2 amount, FaoliComponent? comp = null)
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

    public bool TryCheckFaoliAmount(EntityUid uid, FixedPoint2 amount, bool popup = true)
    {
        if (!TryComp<FaoliComponent>(uid, out var comp))
            return false;

        if (comp.Faoli >= amount)
            return true;

        if (popup)
            _popup.PopupEntity(Loc.GetString("faoli-not-enough"), uid, uid);

        return false;
    }
}
