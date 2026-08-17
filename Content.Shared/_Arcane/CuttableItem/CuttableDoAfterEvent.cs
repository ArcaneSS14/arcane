using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CuttableItem;

[Serializable, NetSerializable]
public sealed partial class CuttableDoAfterEvent : SimpleDoAfterEvent { }
