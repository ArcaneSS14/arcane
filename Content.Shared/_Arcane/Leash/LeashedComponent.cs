using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Leash;

/// <summary>
/// Динамический компонент, который добавляется сущности (игроку/питомцу), когда к ней привязывают поводок.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LeashedComponent : Component
{
    /// <summary>
    /// Ссылка на сам предмет-поводок. Позволяет привязанному человеку знать, кто или что его держит.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public EntityUid? Leash;
}
