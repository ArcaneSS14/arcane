using Content.Shared.Research.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Orion.DocumentPrinter;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DocumentPrinterComponent : Component
{
    [DataField]
    public List<(EntityUid Actor, LatheRecipePrototype Recipe)> Queue { get; set; } = [];

    [DataField]
    public SoundSpecifier SwitchSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    [DataField, AutoNetworkedField]
    public bool IsOnAutocomplete = true;
}

[ByRefEvent]
public readonly struct PrintingDocumentEvent(EntityUid paper, EntityUid actor)
{
    public readonly EntityUid Paper = paper;
    public readonly EntityUid Actor = actor;
}
