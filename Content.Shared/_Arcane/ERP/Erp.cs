namespace Content.Shared._Arcane.ERP;

public enum ErpPreference
{
    Yes = 2,
    Ask = 1,
    No = 0,
}

public sealed class ErpPreferenceChangedEvent(ErpPreference oldPreference, ErpPreference newPreference) : EntityEventArgs
{
    public ErpPreference OldPreference = oldPreference;
    public ErpPreference NewPreference = newPreference;
}
