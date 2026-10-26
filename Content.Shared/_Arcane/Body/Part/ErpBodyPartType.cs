using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.Body.Part;

[Serializable, NetSerializable]
public enum ErpBodyPartType : ushort
{
    None = 0,
    Chest = 1 << 0,
    Groin = 1 << 1,
    Butt = 1 << 2
}
