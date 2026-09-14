using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared._Arcane.Faoli.Events;

public sealed partial class BurstRegenerationEvent : EntityTargetActionEvent
{

    [DataField]
    public float Multiply = 1f;

    [DataField]
    public bool Limited = true;

    [DataField]
    public int Cost = 0;

    [DataField]
    public SoundSpecifier? Sound;
}
