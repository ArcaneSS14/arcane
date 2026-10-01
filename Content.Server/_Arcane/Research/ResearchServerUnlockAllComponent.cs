namespace Content.Server._Arcane.Research;

/// <summary>
/// Unlocks every technology on the research server network when the server is map-initialized.
/// </summary>
[RegisterComponent, Access(typeof(ResearchServerUnlockAllSystem))]
public sealed partial class ResearchServerUnlockAllComponent : Component;
