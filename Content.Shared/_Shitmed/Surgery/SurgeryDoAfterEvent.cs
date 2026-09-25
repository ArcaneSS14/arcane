// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Shitmed.Medical.Surgery;

[Serializable, NetSerializable]
public sealed partial class SurgeryDoAfterEvent : SimpleDoAfterEvent
{
    public readonly EntProtoId Surgery;
    public readonly EntProtoId Step;
    public readonly bool ToolUsed;

    public SurgeryDoAfterEvent(EntProtoId surgery, EntProtoId step, bool toolUsed)
    {
        Surgery = surgery;
        Step = step;
        ToolUsed = toolUsed;
    }

    // Arcane-Start
    /// <summary>
    ///     Two surgery do-afters are duplicates when they run the same surgery step, even on different bodies,
    ///     so callers can locate and cancel competing surgeries explicitly.
    /// </summary>
    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is SurgeryDoAfterEvent surgery
            && Surgery == surgery.Surgery
            && Step == surgery.Step;
    }
    // Arcane-End
}
