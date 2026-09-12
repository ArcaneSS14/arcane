using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared._Arcane.Faoli.Events;

public sealed partial class TouchChangeTemperatureEvent : EntityTargetActionEvent
{
    [DataField(required: true)]
    public float Heat;

    [DataField]
    public bool IgnoreResistance = false;

    [DataField]
    public int Cost = 0;

    [DataField]
    public SoundSpecifier? Sound;
}
