namespace Content.Shared._Arcane.Surgery;

/// <summary>
///     A surgery step that can be done on yourself without the self-surgery slowdown and failure penalty.
/// </summary>
[RegisterComponent]
public sealed partial class SurgerySelfOperableStepComponent : Component { }
