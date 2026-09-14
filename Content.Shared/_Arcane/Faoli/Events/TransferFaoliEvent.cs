using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared._Arcane.Faoli.Events;

public sealed partial class TransfetFaoliEvent : EntityTargetActionEvent
{

    [DataField(required: true)]
    public int Amount;

    [DataField]
    public int Cost = 0;

    [DataField]
    public SoundSpecifier? Sound;
}
