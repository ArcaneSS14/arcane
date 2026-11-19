namespace Content.Server._Arcane.Polymorph;

[RegisterComponent]
public sealed partial class MobTurtleComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsHidden = false;
}
