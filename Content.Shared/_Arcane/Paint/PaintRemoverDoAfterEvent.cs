using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.Paint;

[Serializable, NetSerializable]
public sealed partial class PaintRemoverDoAfterEvent : SimpleDoAfterEvent;
