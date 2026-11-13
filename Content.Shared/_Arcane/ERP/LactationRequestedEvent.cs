using Content.Shared._Arcane.ErpPanel;
using Robust.Shared.GameObjects;

namespace Content.Shared._Arcane.ERP;

public sealed class LactationRequestedEvent
{
    public EntityUid User;

    public EntityUid Target;
    public PanelInteractionPrototype Prototype = default!;
}
