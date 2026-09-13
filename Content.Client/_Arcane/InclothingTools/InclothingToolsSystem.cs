/*using Content.Shared._Arcane.InclothingTools;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Arcane.InclothingTools;

public sealed class InclothingToolsSysem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        //SubscribeLocalEvent<InclothingToolsComponent, SelectInclothingToolEvent>(OnSelectInclothingTool);
    }

    private void OnSelectInclothingTool(Entity<InclothingToolsComponent> entity, ref SelectInclothingToolEvent args)
    {
        if (entity.Comp.Container == null || entity.Comp.Container.Count <= 0)
            return;

        _ui.OpenUi(entity.Owner, SelectInclothingToolUiKey.Key);
    }
}*/
