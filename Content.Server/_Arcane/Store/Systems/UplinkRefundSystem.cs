using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Store.Systems;
using Content.Shared._Arcane.Store.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Store.Components;
using Content.Shared.Throwing;
using Content.Shared.Trigger.Components;
using Content.Shared.Verbs;
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
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UplinkRefundableComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<UplinkRefundableComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<UplinkRefundableComponent, ThrowEvent>(OnThrown);
        SubscribeLocalEvent<UplinkRefundableComponent, AfterInteractEvent>(OnAfterInteract,
            after: new[] { typeof(SharedTetherGunSystem) });
        SubscribeLocalEvent<StoreComponent, InteractUsingEvent>(OnUplinkInteractUsing);
        SubscribeLocalEvent<UplinkRefundableComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
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

    private void OnGetVerbs(Entity<UplinkRefundableComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (ent.Comp.IsUsed)
            return;

        var user = args.User;
        var target = ent.Owner;

        if (!TryGetUplinkForUser(user, out var uplink, out _))
            return;

        if (ent.Comp.OriginalUplink != uplink)
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("uplink-refund-verb"),
            Act = () =>
            {
                if (TryGetUplinkForUser(user, out var currentUplink, out var store))
                    TryRefund(target, ent.Comp, currentUplink, store, user);
                else
                    _popup.PopupEntity(Loc.GetString("uplink-refund-fail-no-uplink"), target, user);
            },
        });
    }

    private bool TryGetUplinkForUser(EntityUid user, out EntityUid uplink, out StoreComponent store)
    {
        uplink = default;
        store = default!;

        if (!_mind.TryGetMind(user, out var userMind, out _))
            return false;

        if (_hands.TryGetActiveItem(user, out var held) && TryComp<StoreComponent>(held, out var heldStore))
        {
            if (heldStore.AccountOwner == null || heldStore.AccountOwner == userMind)
            {
                uplink = held.Value;
                store = heldStore;
                return true;
            }
        }

        if (TryComp<ImplantedComponent>(user, out var implanted))
        {
            var container = implanted.ImplantContainer;
            if (container != null)
            {
                foreach (var implant in container.ContainedEntities)
                {
                    if (!TryComp<StoreComponent>(implant, out var implantStore))
                        continue;

                    if (implantStore.AccountOwner != null && implantStore.AccountOwner != userMind)
                        continue;

                    uplink = implant;
                    store = implantStore;
                    return true;
                }
            }
        }

        return false;
    }

    private void OnUplinkInteractUsing(Entity<StoreComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var used = args.Used;

        if (!TryComp<UplinkRefundableComponent>(used, out var refundable))
            return;

        if (TryRefund(used, refundable, ent.Owner, ent.Comp, args.User))
            args.Handled = true;
    }

    private bool TryRefund(EntityUid used, UplinkRefundableComponent refundable, EntityUid uplink, StoreComponent store, EntityUid user)
    {
        if (refundable.IsUsed)
        {
            _popup.PopupEntity(Loc.GetString("uplink-refund-fail-used"), used, user);
            return false;
        }

        if (refundable.OriginalUplink != uplink)
        {
            _popup.PopupEntity(Loc.GetString("uplink-refund-fail-wrong-uplink"), used, user);
            return false;
        }

        if (!_hands.TryGetActiveItem(user, out var activeItem) || activeItem != used)
        {
            _popup.PopupEntity(Loc.GetString("uplink-refund-fail-not-in-hand"), used, user);
            return false;
        }

        if (refundable.RefundAmount <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("uplink-refund-fail-zero"), used, user);
            return false;
        }

        var refundAmount = refundable.RefundAmount;
        var currencyId = refundable.Currency.Id;

        QueueDel(used);

        var currency = new Dictionary<string, FixedPoint2>
        {
            { currencyId, refundAmount }
        };
        _store.TryAddCurrency(currency, uplink, store);

        _popup.PopupEntity(
            Loc.GetString("uplink-refund-success", ("amount", refundAmount)),
            uplink,
            user);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg"), uplink);

        return true;
    }
}
