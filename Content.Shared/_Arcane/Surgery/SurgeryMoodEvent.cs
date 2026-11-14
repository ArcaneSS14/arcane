using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

[ByRefEvent]
public readonly record struct SurgeryMoodEvent(SurgeryMoodOutcome Outcome, EntProtoId Step);

public enum SurgeryMoodOutcome : byte
{
    Started,
    Success,
    Failure,
}
