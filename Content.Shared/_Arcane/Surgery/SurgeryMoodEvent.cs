namespace Content.Shared._Arcane.Surgery;

[ByRefEvent]
public readonly record struct SurgeryMoodEvent(SurgeryMoodOutcome Outcome);

public enum SurgeryMoodOutcome : byte
{
    Started,
    Success,
    Failure,
}
