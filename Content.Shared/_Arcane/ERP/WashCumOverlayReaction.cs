using Content.Shared.EntityEffects;

namespace Content.Shared._Arcane.ERP;

public sealed partial class WashCumOverlayReactionSystem : EntityEffectSystem<CumOverlayComponent, WashCumOverlayReaction>
{
    protected override void Effect(Entity<CumOverlayComponent> entity, ref EntityEffectEvent<WashCumOverlayReaction> args)
    {
        RemComp<CumOverlayComponent>(entity);
    }
}

public sealed partial class WashCumOverlayReaction : EntityEffectBase<WashCumOverlayReaction>;
