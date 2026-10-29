using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.EntitySpawning;

/// <summary>
/// Spawns a number of entities of a given prototype at the coordinates of this entity.
/// Amount is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class SpawnEntityEntityEffectSystem : EntityEffectSystem<TransformComponent, SpawnEntity>
{
    [Dependency] private readonly INetManager _net = default!;
    // Arcane-Start
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    // Arcane-End

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<SpawnEntity> args)
    {
        var quantity = args.Effect.ShouldScale ? args.Effect.Number * (int) Math.Floor(args.Scale) : args.Effect.Number; // Goobstation - Added ShouldSCale
        var proto = args.Effect.Entity;

        if (args.Effect.Predicted)
        {
            for (var i = 0; i < quantity; i++)
            {
                RestoreCollision(PredictedSpawnNextToOrDrop(proto, entity, entity.Comp), proto); // Arcane-Edit
            }
        }
        else if (_net.IsServer)
        {
            for (var i = 0; i < quantity; i++)
            {
                RestoreCollision(SpawnNextToOrDrop(proto, entity, entity.Comp), proto); // Arcane-Edit
            }
        }
    }

    // Arcane-Start
    // SpawnNextToOrDrop initializes the entity in nullspace, where physics turns collision off and never turns it back on.
    private void RestoreCollision(EntityUid uid, EntProtoId proto)
    {
        if (_prototype.Index(proto).TryGetComponent<PhysicsComponent>(out var physics, Factory) && physics.CanCollide)
            _physics.SetCanCollide(uid, true);
    }
    // Arcane-End
}

/// <inheritdoc cref="BaseSpawnEntityEntityEffect{T}"/>
public sealed partial class SpawnEntity : BaseSpawnEntityEntityEffect<SpawnEntity>;
