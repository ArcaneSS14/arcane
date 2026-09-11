namespace Content.Server._Arcane.Mech;

[RegisterComponent, Access(typeof(MechVoiceSystem))]
public sealed partial class MechVoiceComponent : Component
{
    [DataField]
    public string? Effect = "robotic";
}
