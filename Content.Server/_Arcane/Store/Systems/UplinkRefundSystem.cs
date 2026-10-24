using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Store.Systems;
using Content.Shared._Arcane.Store.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Store.Components;
using Content.Shared.Throwing;
using Content.Shared.Trigger.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Misc;
using Content.Shared.Weapons.Ranged.Components;
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
        SubscribeLocalEvent<UplinkRefundableComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<UplinkRefundableComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<UplinkRefundableComponent, ThrowEvent>(OnThrown);
        SubscribeLocalEvent<UplinkRefundableComponent, AfterInteractEvent>(OnAfterInteract,
            after: new[] { typeof(SharedTetherGunSystem) });
        SubscribeLocalEvent<UplinkRefundableComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<StoreComponent, InteractUsingEvent>(OnUplinkInteractUsing);
    }

    private void MarkUsed(EntityUid uid, UplinkRefundableComponent comp)
    {
        if (comp.IsUsed)
            return;

        comp.IsUsed = true;
    }

    private void OnGunShot(Entity<UplinkRefundableComponent> ent, ref GunShotEvent args)
    {
        if (args.Ammo.Count == 0)
            return;

        MarkUsed(ent, ent.Comp);
    }

    private void OnMeleeHit(Entity<UplinkRefundableComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        MarkUsed(ent, ent.Comp);
    }

    private void OnThrown(Entity<UplinkRefundableComponent> ent, ref ThrowEvent args)
    {
        MarkUsed(ent, ent.Comp);
    }

    private void OnAfterInteract(Entity<UplinkRefundableComponent> ent, ref AfterInteractEvent args)
    {
        if (HasComp<GunComponent>(ent) || HasComp<TimerTriggerComponent>(ent))
            return;

        if (TryComp<TetherGunComponent>(ent, out var tether) && tether.Tethered != null)
        {
            MarkUsed(ent, ent.Comp);
            return;
        }

        if (TryComp<ForceGunComponent>(ent, out var force) && force.Tethered != null)
        {
            MarkUsed(ent, ent.Comp);
            return;
        }

        if (args.Handled)
            MarkUsed(ent, ent.Comp);
    }

    private void OnUseInHand(Entity<UplinkRefundableComponent> ent, ref UseInHandEvent args)
    {
        if (HasComp<GunComponent>(ent))
            return;

        if (HasComp<TimerTriggerComponent>(ent))
            return;

        if (args.Handled)
            MarkUsed(ent, ent.Comp);
    }

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
