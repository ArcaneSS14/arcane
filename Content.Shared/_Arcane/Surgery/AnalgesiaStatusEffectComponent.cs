using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Surgery;

/// <summary>
///     Painkillers on a status effect entity: surgery on the patient goes as fast as under anesthesia.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AnalgesiaStatusEffectComponent : Component { }
