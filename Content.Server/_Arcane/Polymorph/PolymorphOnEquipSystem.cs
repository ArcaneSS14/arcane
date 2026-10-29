using Content.Server.Polymorph.Systems;
using Content.Shared.Clothing;

namespace Content.Server._Arcane.Polymorph;

public sealed class PolymorphOnEquipSystem : EntitySystem
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PolymorphOnEquipComponent, ClothingDidEquippedEvent>(OnEquipped);
    }

    private void OnEquipped(EntityUid uid, PolymorphOnEquipComponent component, ref ClothingDidEquippedEvent args)
    {
        var protoId = MetaData(args.Clothing).EntityPrototype?.ID;
        if (protoId == null || protoId != component.RequiredProto)
            return;
        _polymorph.PolymorphEntity(uid, component.PolymorphProto);
    }
}
