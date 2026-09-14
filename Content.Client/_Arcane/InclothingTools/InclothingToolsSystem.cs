using Content.Shared._Arcane.InclothingTools;
using Robust.Client.GameObjects;

namespace Content.Client._Arcane.InclothingTools;

public sealed class InclothingToolsSysem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InclothingToolsComponent, RandomInclothingToolEvent>(OnRandomInclothingTool);
    }

    private void OnRandomInclothingTool(Entity<InclothingToolsComponent> entity, ref RandomInclothingToolEvent args)
    {
        var message = new RandomInclothingToolEvent()
        {
            Clothing = GetNetEntity(entity.Owner)
        };

        RaiseNetworkEvent(message);
    }
}
