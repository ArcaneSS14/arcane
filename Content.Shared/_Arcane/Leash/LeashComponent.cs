using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Leash;

/// <summary>
/// Компонент поводка
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LeashComponent : Component
{
    /// <summary>
    /// Сущность к которой сейчас привязан поводок
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? AttachedEntity;

    /// <summary>
    /// Максимальная дистанция поводка
    /// </summary>
    [DataField]
    public float MaxDistance = 3.0f;

    /// <summary>
    /// ID соединения
    /// </summary>
    [DataField]
    public string? JointId;
}
