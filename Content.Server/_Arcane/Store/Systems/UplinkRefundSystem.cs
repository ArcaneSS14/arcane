using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Store.Systems;
using Content.Shared._Arcane.Store.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Store.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Arcane.Store.Systems;

public sealed class UplinkRefundSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UplinkRefundableComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<UplinkRefundableComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<UplinkRefundableComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<UplinkRefundableComponent, AttemptShootEvent>(OnShootAttempt);
        SubscribeLocalEvent<UplinkRefundableComponent, ActionPerformedEvent>(OnActionPerformed);
        SubscribeLocalEvent<StoreComponent, InteractUsingEvent>(OnUplinkInteractUsing);
    }

    private void MarkUsed(EntityUid uid, UplinkRefundableComponent comp)
    {
        if (comp.IsUsed)
            return;

        comp.IsUsed = true;
        Dirty(uid, comp);
    }

    private void OnUseInHand(Entity<UplinkRefundableComponent> ent, ref UseInHandEvent args)
        => MarkUsed(ent, ent.Comp);

    private void OnActivateInWorld(Entity<UplinkRefundableComponent> ent, ref ActivateInWorldEvent args)
        => MarkUsed(ent, ent.Comp);

    private void OnAfterInteract(Entity<UplinkRefundableComponent> ent, ref AfterInteractEvent args)
        => MarkUsed(ent, ent.Comp);

    private void OnShootAttempt(Entity<UplinkRefundableComponent> ent, ref AttemptShootEvent args)
    {
        if (!args.Cancelled)
            MarkUsed(ent, ent.Comp);
    }

    private void OnActionPerformed(Entity<UplinkRefundableComponent> ent, ref ActionPerformedEvent args)
        => MarkUsed(ent, ent.Comp);

    private void OnUplinkInteractUsing(Entity<StoreComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var used = args.Used;

        if (!TryComp<UplinkRefundableComponent>(used, out var refundable))
            return;

        var user = args.User;
        if (refundable.IsUsed)
        {
            _popup.PopupEntity(Loc.GetString("uplink-refund-fail-used"), used, user);
            return;
        }
        if (refundable.OriginalUplink != ent.Owner)
        {
            _popup.PopupEntity(Loc.GetString("uplink-refund-fail-wrong-uplink"), used, user);
            return;
        }
        if (!_hands.TryGetActiveItem(user, out var activeItem) || activeItem != used)
        {
            _popup.PopupEntity(Loc.GetString("uplink-refund-fail-not-in-hand"), used, user);
            return;
        }
        if (refundable.RefundAmount <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("uplink-refund-fail-zero"), used, user);
            return;
        }
        var refundAmount = refundable.RefundAmount;
        var currencyId = refundable.Currency.Id;
        QueueDel(used);
        var currency = new Dictionary<string, FixedPoint2>
        {
            { currencyId, refundAmount }
        };
        _store.TryAddCurrency(currency, ent.Owner, ent.Comp);

        _popup.PopupEntity(
            Loc.GetString("uplink-refund-success", ("amount", refundAmount)),
            ent.Owner,
            user);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg"), ent.Owner);

        args.Handled = true;
    }
}
