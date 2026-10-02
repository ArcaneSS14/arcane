using Content.Server._Arcane.Changeling.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Changeling;

public sealed class ChangelingCocoonSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly EntProtoId Cocoon = "ChangelingCocoon";
    private static readonly ProtoId<DamageTypePrototype> DrainDamage = "Cellular";
    private const string ContainerId = "cocoon";

    public void Drain(EntityUid target)
    {
        EnsureComp<ChangelingDrainedComponent>(target);
        _damage.TryChangeDamage(target, new DamageSpecifier(_proto.Index(DrainDamage), 100), true, false, targetPart: TargetBodyPart.All);

        var cocoon = Spawn(Cocoon, _transform.GetMoverCoordinates(target));
        var container = _container.EnsureContainer<ContainerSlot>(cocoon, ContainerId);
        if (!_container.Insert(target, container))
            Del(cocoon);
    }
}
