using Content.Shared.EntityEffects;

namespace Content.Shared._Arcane.ERP;

public sealed partial class RemoveCumWallEffectSystem : EntityEffectSystem<MetaDataComponent, RemoveCumWallEffect>
{
    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<RemoveCumWallEffect> args)
    {
        QueueDel(entity);
    }
}

public sealed partial class RemoveCumWallEffect : EntityEffectBase<RemoveCumWallEffect>;
