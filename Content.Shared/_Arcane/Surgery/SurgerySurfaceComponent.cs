namespace Content.Shared._Arcane.Surgery;

/// <summary>
///     Speeds up surgery on a patient buckled to or lying on this entity.
/// </summary>
[RegisterComponent]
public sealed partial class SurgerySurfaceComponent : Component
{
    [DataField]
    public float SpeedModifier = 1f;
}
