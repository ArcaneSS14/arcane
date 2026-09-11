using Robust.Shared.Prototypes;
using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared._Arcane.Faoli.Events;

public sealed partial class SpawnnItemInHandEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public bool Force = true;

    [DataField]
    public bool Unremovable = true;

    [DataField]
    public bool DeleteOnDrop = true;

    [DataField]
    public float TimedDespawn = 0f;

    [DataField]
    public int Cost = 0;

    [DataField]
    public SoundSpecifier? Sound;
}
