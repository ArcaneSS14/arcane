using Robust.Shared.Serialization;

namespace Content.Shared.SpecialWhitelist;

/// <summary>
/// Структура для хранения списка разрешенных сикеев в прототипах кастомизации.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class SpecialWhiteList
{
    [DataField("allowed")]
    public List<string> Allowed { get; private set; } = new();
}
