namespace Content.Shared._Arcane.Surgery;

[ByRefEvent]
public readonly record struct SurgeryStepBotchedEvent(EntityUid User, EntityUid Body, EntityUid Part);
