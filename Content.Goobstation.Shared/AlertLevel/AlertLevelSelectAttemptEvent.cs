namespace Content.Goobstation.Shared.AlertLevel;

[ByRefEvent]
public record struct AlertLevelSelectAttemptEvent(EntityUid Station, EntityUid Console, EntityUid User, string Level)
{
    public bool Cancelled;
}

// Arcane-Start
/// <summary>
/// Requests that a station's alert-level gate is unlocked.
/// </summary>
[ByRefEvent]
public record struct AlertLevelGateUnlockRequestEvent(EntityUid Station, bool AnnounceToStation)
{
    public bool Unlocked;
}

/// <summary>
/// Raised after a station's alert-level gate is unlocked.
/// </summary>
[ByRefEvent]
public record struct AlertLevelGateUnlockedEvent(EntityUid Station);
// Arcane-End
