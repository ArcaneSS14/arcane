using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Leash;

/// <summary>
/// Компонент ошейника. Позволяет привязывать LeashComponent к сущности, которая носит этот предмет.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CollarComponent : Component
{
    // В будущем сюда можно добавить кастомные звуки (например, звон бубенчика при ходьбе) 
    // или специфичные взаимодействия, но пока он может быть пустым маркером.
}
