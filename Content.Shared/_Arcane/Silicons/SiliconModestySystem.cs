using Content.Shared._Arcane.Silicons;
using Content.Shared.Actions;
using Robust.Shared.Utility;

namespace Content.Server._Arcane.Silicons;

public sealed class SiliconModestySystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    private static readonly SpriteSpecifier RsiCovered = new SpriteSpecifier.Rsi(new ResPath("_Arcane/Interface/Actions/silicon_modesty.rsi"), "covered");
    private static readonly SpriteSpecifier RsiUncovered = new SpriteSpecifier.Rsi(new ResPath("_Arcane/Interface/Actions/silicon_modesty.rsi"), "uncovered");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleSiliconModestyEvent>(OnToggleModesty);
    }

    private void OnToggleModesty(ToggleSiliconModestyEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;

        if (HasComp<SiliconModestyComponent>(performer))
        {
            RemComp<SiliconModestyComponent>(performer);
            _actions.SetIcon(args.Action.AsNullable(), RsiUncovered);
        }
        else
        {
            EnsureComp<SiliconModestyComponent>(performer);
            _actions.SetIcon(args.Action.AsNullable(), RsiCovered);
        }

        args.Handled = true;
    }
}
