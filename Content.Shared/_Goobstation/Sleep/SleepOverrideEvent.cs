using Content.Shared.Mobs;

namespace Content.Shared._Goobstation.Sleep;

/// <summary>
/// Raised whenever entity almost went to sleep
/// </summary>
[ByRefEvent]
// Arcane-Edit-Start
public record struct SleepOverrideEvent    //(MobState MobState = MobState.Alive);
{
    public SleepOverrideEvent()
    {
        MobState = MobState.Alive;
    }

    public MobState MobState;
}
// Arcane-Edit-End
