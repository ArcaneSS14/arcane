using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Store.Systems;
using Content.Shared._Arcane.Store.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
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
using Robust.Shared.Containers;

namespace Content.Server._Arcane.Store.Systems;

public sealed class UplinkRefundSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

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

        if (!TryGetUplinkForUser(user, ent.Comp.OriginalUplink, out var uplink, out var store))
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("uplink-refund-verb"),
            Act = () =>
            {
                TryRefund(target, ent.Comp, uplink, store, user);
            },
        });
    }

    private bool TryGetUplinkForUser(EntityUid user, EntityUid originalUplink, out EntityUid uplink, out StoreComponent store)
    {
        uplink = default;
        store = default!;

        if (!_mind.TryGetMind(user, out var userMind, out _))
            return false;

        if (_hands.TryGetActiveItem(user, out var held) && held.Value == originalUplink &&
            TryComp<StoreComponent>(held, out var heldStore) &&
            (heldStore.AccountOwner == null || heldStore.AccountOwner == userMind))
        {
            uplink = held.Value;
            store = heldStore;
            return true;
        }

        if (!TryComp<ImplantedComponent>(user, out var implanted))
            return false;

        var container = implanted.ImplantContainer;
        if (container == null)
            return false;

        foreach (var implant in container.ContainedEntities)
        {
            if (implant != originalUplink)
                continue;

            if (!TryComp<StoreComponent>(implant, out var implantStore))
                return false;

            if (implantStore.AccountOwner != null && implantStore.AccountOwner != userMind)
                return false;

            uplink = implant;
            store = implantStore;
            return true;
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

    private bool CanRefundItem(EntityUid used, UplinkRefundableComponent refundable, EntityUid user)
    {
        if (refundable.OriginalContainerPrototypes != null)
        {
            var currentPrototypes = new List<string>();
            if (TryComp<ContainerManagerComponent>(used, out var containerManager))
            {
                foreach (var container in containerManager.Containers.Values)
                {
                    foreach (var entity in container.ContainedEntities)
                    {
                        var meta = MetaData(entity);
                        if (meta.EntityPrototype != null)
                            currentPrototypes.Add(meta.EntityPrototype.ID);
                    }
                }
            }

            currentPrototypes.Sort();
            var originalPrototypes = new List<string>(refundable.OriginalContainerPrototypes);
            originalPrototypes.Sort();

            if (!((IEnumerable<string>) currentPrototypes).SequenceEqual(originalPrototypes))
            {
                _popup.PopupEntity(Loc.GetString("uplink-refund-fail-container-changed"), used, user);
                return false;
            }
        }

        if (refundable.OriginalSolutionReagents != null)
        {
            var currentReagents = new Dictionary<string, FixedPoint2>();
            if (TryComp<SolutionContainerManagerComponent>(used, out var solutionManager))
            {
                foreach (var (_, soln) in _solutionContainer.EnumerateSolutions((used, solutionManager)))
                {
                    foreach (var (reagent, quantity) in soln.Comp.Solution.Contents)
                    {
                        var id = reagent.Prototype;
                        if (currentReagents.TryGetValue(id, out var existing))
                            currentReagents[id] = existing + quantity;
                        else
                            currentReagents[id] = quantity;
                    }
                }
            }

            if (currentReagents.Count != refundable.OriginalSolutionReagents.Count)
            {
                _popup.PopupEntity(Loc.GetString("uplink-refund-fail-solution-changed"), used, user);
                return false;
            }

            foreach (var (reagentId, originalQuantity) in refundable.OriginalSolutionReagents)
            {
                if (!currentReagents.TryGetValue(reagentId, out var currentQuantity))
                {
                    _popup.PopupEntity(Loc.GetString("uplink-refund-fail-solution-changed"), used, user);
                    return false;
                }

                if (currentQuantity != originalQuantity)
                {
                    _popup.PopupEntity(Loc.GetString("uplink-refund-fail-solution-changed"), used, user);
                    return false;
                }
            }
        }

        if (TryComp<BallisticAmmoProviderComponent>(used, out var ballistic))
        {
            var currentCount = ballistic.Entities.Count + ballistic.UnspawnedCount;
            if (currentCount < ballistic.Capacity)
            {
                _popup.PopupEntity(Loc.GetString("uplink-refund-fail-magazine-empty"), used, user);
                return false;
            }
        }

        if (TryComp<CartridgeAmmoComponent>(used, out var cartridge) && cartridge.Spent)
        {
            _popup.PopupEntity(Loc.GetString("uplink-refund-fail-cartridge-spent"), used, user);
            return false;
        }

        return true;
    }

    private bool TryRefund(EntityUid used, UplinkRefundableComponent refundable, EntityUid uplink, StoreComponent store, EntityUid user)
    {
        if (refundable.IsUsed)
        {
            _popup.PopupEntity(Loc.GetString("uplink-refund-fail-used"), used, user);
            return false;
        }

        if (!CanRefundItem(used, refundable, user))
            return false;

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

        var currency = new Dictionary<string, FixedPoint2>
        {
            { currencyId, refundAmount }
        };
        if (!_store.TryAddCurrency(currency, uplink, store))
        {
            _popup.PopupEntity(Loc.GetString("uplink-refund-fail-currency"), used, user);
            return false;
        }

        refundable.IsUsed = true;
        QueueDel(used);

        _popup.PopupEntity(
            Loc.GetString("uplink-refund-success", ("amount", refundAmount)),
            uplink,
            user);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg"), uplink);

        return true;
    }
}
