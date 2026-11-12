using Robust.Shared.Network;
using Robust.Shared.GameObjects;

namespace Content.Shared._Arcane.GameTicking;
// ивент подсчёта количества офицеров сб
public sealed class SecurityBudgetEvent : EntityEventArgs
{
    public NetUserId[] Officers { get; }
    public bool LateJoin { get; }

    public SecurityBudgetEvent(int officers)
    {
        Officers = officers;
        LateJoin = lateJoin;
    }
}
