namespace Content.Server._Arcane.AlertLevel;

[RegisterComponent]
public sealed partial class AlertLevelGateConsoleComponent : Component
{
    [DataField]
    public bool Enabled = true;

    [DataField]
    public bool CentComm;
}
