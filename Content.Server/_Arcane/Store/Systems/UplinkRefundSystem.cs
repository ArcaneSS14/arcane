using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Store.Systems;
using Content.Shared._Arcane.Store.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Store.Components;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Misc;
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
        SubscribeLocalEvent<UplinkRefundableComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<StoreComponent, InteractUsingEvent>(OnUplinkInteractUsing);
    }

    private void MarkUsed(EntityUid uid, UplinkRefundableComponent comp)
    {
        if (comp.IsUsed)
            return;

        comp.IsUsed = true;
        Dirty(uid, comp);
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
        if (!args.Handled)
            return;
        if (!HasComp<TetherGunComponent>(ent) && !HasComp<ForceGunComponent>(ent))
            return;

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
