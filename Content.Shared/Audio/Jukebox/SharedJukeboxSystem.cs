// SPDX-License-Identifier: MIT

using Robust.Shared.Audio.Systems;

namespace Content.Shared.Audio.Jukebox;

public abstract class SharedJukeboxSystem : EntitySystem
{
    [Dependency] protected readonly SharedAudioSystem Audio = default!;

    // Orion-Start
    public static float MapToRange(float value, float leftMin, float leftMax, float rightMin, float rightMax)
    {
        return rightMin + (value - leftMin) * (rightMax - rightMin) / (leftMax - leftMin);
    }
    // Orion-End

    // Arcane-Start
    public static float GetAudioVolume(JukeboxComponent component)
    {
        return MapToRange(component.Volume, component.MinSlider, component.MaxSlider,
            component.MinVolume, component.MaxVolume);
    }
    // Arcane-End
}
