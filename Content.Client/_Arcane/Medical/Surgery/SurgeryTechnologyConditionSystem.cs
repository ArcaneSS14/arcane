using Content.Shared._Arcane.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Robust.Client.GameObjects;

namespace Content.Client._Arcane.Medical.Surgery;

// The research server is outside the client's view, so gated surgeries follow the server's last choices when known.
public sealed class SurgeryTechnologyConditionSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryTechnologyConditionComponent, SurgeryValidEvent>(OnValid);
    }

    private void OnValid(Entity<SurgeryTechnologyConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (args.Cancelled
            || Prototype(ent) is not { } surgery
            || !_ui.TryGetUiState<SurgeryBuiState>(args.Body, SurgeryUIKey.Key, out var state)
            || !state.Choices.TryGetValue(GetNetEntity(args.Part), out var choices))
            return;

        if (!choices.Contains(surgery.ID))
            args.Cancelled = true;
    }
}
