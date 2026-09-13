using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared._Arcane.Faoli.Events;

public sealed partial class DemonicMessageEvent : EntityTargetActionEvent
{
    [DataField]
    public int Cost = 0;

    [DataField]
    public SoundSpecifier? Sound;
}
