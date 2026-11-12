namespace Content.Shared._Arcane.GameTicking;
// ивент подсчёта количества офицеров сб
public sealed class SecurityBudgetEvent : EntityEventArgs
{
    public int Officers { get; }

    public SecurityBudgetEvent(int officers)
    {
        Officers = officers;
    }
}
