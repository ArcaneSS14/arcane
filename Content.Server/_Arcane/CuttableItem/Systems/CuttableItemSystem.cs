using Content.Server.Radio.EntitySystems;
using Content.Shared.Radio;
using Content.Shared._Arcane.CuttableItem.Components;
using Robust.Shared.Prototypes;


namespace Content.Server._Arcane.CuttableItem.Systems;

public sealed class CuttableItemSystem : EntitySystem
{
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CuttableItemComponent, CuttableCutEvent>(OnItemCut);
    }

    private void OnItemCut(EntityUid uid, CuttableItemComponent component, CuttableCutEvent args)
    {
        if (!_prototypeManager.TryIndex<RadioChannelPrototype>(component.RadioChannel, out var channel))
            return;

        var userName = Name(args.User);
        var userItem = Name(args.Item);

        var message = Loc.GetString(component.AlertMessage, ("user", userName), ("item", userItem));

        _radio.SendRadioMessage(uid, message, channel, uid);
    }
}
