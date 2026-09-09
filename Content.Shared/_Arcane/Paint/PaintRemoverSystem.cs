using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Shared.DoAfter;
using Content.Shared.Verbs;
using Content.Shared.Sprite;
using Content.Shared.Inventory;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Arcane.Paint;

public sealed class PaintRemoverSystem : SharedPaintSystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaintRemoverComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<PaintRemoverComponent, PaintRemoverDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<PaintRemoverComponent, GetVerbsEvent<UtilityVerb>>(OnPaintRemoveVerb);
    }


    private void OnInteract(EntityUid uid, PaintRemoverComponent component, AfterInteractEvent args)
    {
        if (args.Handled
            || !args.CanReach
            || args.Target is not { Valid: true } target
            || !HasComp<ArcanePaintedComponent>(target))
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.CleanDelay, new PaintRemoverDoAfterEvent(), uid, args.Target, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            MovementThreshold = 1.0f,
        });
        args.Handled = true;
    }

    private void OnDoAfter(EntityUid uid, PaintRemoverComponent component, DoAfterEvent args)
    {
        if (args.Cancelled
            || args.Handled
            || args.Args.Target is not { Valid: true } target
            || !HasComp<ArcanePaintedComponent>(target))
            return;

        _audio.PlayPredicted(component.Sound, target, args.User);
        _popup.PopupClient(Loc.GetString("paint-removed", ("target", target)), args.User, args.User, PopupType.Medium);
        RemComp<ArcanePaintedComponent>(target);
        _appearanceSystem.SetData(target, PaintVisuals.Painted, false);

        if (HasComp<InventoryComponent>(target)
            && _inventory.TryGetSlots(target, out var slotDefinitions))
            foreach (var slot in slotDefinitions)
            {
                if (!_inventory.TryGetSlotEntity(target, slot.Name, out var slotEnt)
                    || !HasComp<ArcanePaintedComponent>(slotEnt.Value))
                    continue;

                RemComp<ArcanePaintedComponent>(slotEnt.Value);
                _appearanceSystem.SetData(slotEnt.Value, PaintVisuals.Painted, false);
            }

        args.Handled = true;
    }

    private void OnPaintRemoveVerb(EntityUid uid, PaintRemoverComponent component, GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !HasComp<ArcanePaintedComponent>(args.Target))
            return;

        var verb = new UtilityVerb()
        {
            Text = Loc.GetString("paint-remove-verb"),
            Act = () =>
            {
                _doAfter.TryStartDoAfter(
                    new DoAfterArgs(
                        EntityManager,
                        args.User,
                        component.CleanDelay,
                        new PaintRemoverDoAfterEvent(),
                        uid,
                        args.Target,
                        uid)
                    {
                        BreakOnMove = true,
                        BreakOnDamage = true,
                        MovementThreshold = 1.0f,
                    });
            },
        };

        args.Verbs.Add(verb);
    }
}
