namespace Content.Server._Arcane.Polymorph;

[RegisterComponent]
public sealed partial class PettingAnimationComponent : Component
{
    [DataField("duration")]
    public float Duration = 0.5f;
}
