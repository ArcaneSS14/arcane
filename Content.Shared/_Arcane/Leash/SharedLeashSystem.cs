using Content.Shared.Clothing.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Arcane.Leash;

/// <summary>
/// General system for managing leash and collar logic
/// Responsible for physically tying entities, handling contextual actions, and syncing
/// </summary>
public abstract class SharedLeashSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LeashComponent, AfterInteractEvent>(OnLeashAfterInteract);
        SubscribeLocalEvent<LeashComponent, UseInHandEvent>(OnLeashUseInHand);
        SubscribeLocalEvent<LeashComponent, GetVerbsEvent<InteractionVerb>>(AddLeashVerbs);
        SubscribeLocalEvent<CollarComponent, GotUnequippedEvent>(OnCollarUnequipped);
        // Deletion
        SubscribeLocalEvent<LeashComponent, ComponentShutdown>(OnLeashShutdown);
        SubscribeLocalEvent<LeashedComponent, ComponentShutdown>(OnLeashedShutdown);
    }

    /// <summary>
    /// Detaches the leash when activated in hand
    /// </summary>
    private void OnLeashUseInHand(EntityUid uid, LeashComponent component, ref UseInHandEvent args)
    {
        // We stop execution if the event has already been handled or the leash isn’t attached to anyone
        if (args.Handled || component.AttachedEntity == null)
            return;

        // Attempt to detach the leash indicating the initiator 
        if (TryDetachLeash(uid, component, user: args.User))
        {
            args.Handled = true; // Marking the event as successfully completed
        }
    }

    /// <summary>
    /// Forms a connection between the leash and the collar when you left-click on the target
    /// </summary>
    private void OnLeashAfterInteract(EntityUid uid, LeashComponent component, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        // If the leash is already tied to something else, we don't do anything
        if (component.AttachedEntity != null)
            return;

        if (TryAttachLeash(uid, args.User, target, component))
        {
            args.Handled = true;
        }
    }

    /// <summary>
    /// Adds the option to Unleash the leash in the right-click interaction menu
    /// </summary>
    private void AddLeashVerbs(EntityUid uid, LeashComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (component.AttachedEntity != null)
        {
            InteractionVerb verb = new()
            {
                Text = Loc.GetString("leash-verb-detach"),
                Act = () => TryDetachLeash(uid, component, user: args.User)
            };
            args.Verbs.Add(verb);
        }
    }

    /// <summary>
    /// Breaks the connection if you take off the collar
    /// </summary>
    private void OnCollarUnequipped(EntityUid uid, CollarComponent component, GotUnequippedEvent args)
    {
        if (TryComp<LeashedComponent>(args.Equipee, out var leashed) && leashed.Leash != null)
        {
            TryDetachLeash(leashed.Leash.Value);
        }
    }

    /// <summary>
    /// When you remove the leash, it breaks the connection
    /// </summary>
    private void OnLeashShutdown(EntityUid uid, LeashComponent component, ComponentShutdown args)
    {
        TryDetachLeash(uid, component);
    }

    /// <summary>
    /// When deleting a linked entity, it breaks the connection
    /// </summary>
    private void OnLeashedShutdown(EntityUid uid, LeashedComponent component, ComponentShutdown args)
    {
        if (component.Leash is { } targetLeashUid)
        {
            TryDetachLeash(targetLeashUid, user: null);
        }
    }

    /// <summary>
    /// Checks if there is a collar
    /// </summary>
    public bool TryGetEquippedCollar(EntityUid target, out EntityUid collarUid)
    {
        collarUid = default;

        // If the object is a collar
        if (HasComp<CollarComponent>(target))
        {
            collarUid = target;
            return true;
        }

        // If the object is in the NECK slot on the entity
        if (_inventorySystem.TryGetSlotEntity(target, "neck", out var neckItem) && HasComp<CollarComponent>(neckItem))
        {
            collarUid = neckItem.Value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Leash attachment system with collar. Checks compliance with conditions
    /// </summary>
    public virtual bool TryAttachLeash(EntityUid leashUid, EntityUid userUid, EntityUid targetUid, LeashComponent? leash = null)
    {
        if (!Resolve(leashUid, ref leash))
            return false;

        // Forbids you from tying yourself up
        var targetEntity = targetUid;
        if (HasComp<CollarComponent>(targetUid) && _containerSystem.TryGetContainingContainer(targetUid, out var container))
        {
            targetEntity = container.Owner;
        }

        // We don't let ourselves get tied down
        if (userUid == targetEntity)
        {
            _popupSystem.PopupClient(Loc.GetString("leash-popup-self-attach"), userUid, userUid);
            return false;
        }

        // We don’t let it be tied up if it doesn’t have a collar
        if (!HasComp<PhysicsComponent>(userUid) || !HasComp<PhysicsComponent>(targetEntity))
            return false;

        if (!TryGetEquippedCollar(targetEntity, out _))
        {
            _popupSystem.PopupClient(Loc.GetString("leash-popup-no-collar"), userUid, userUid);
            return false;
        }

        // We don’t allow linking if there’s already a connection
        if (HasComp<LeashedComponent>(targetEntity))
        {
            _popupSystem.PopupClient(Loc.GetString("leash-popup-already-leashed"), userUid, userUid);
            return false;
        }

        // We save the links and create a unique ID for the connection
        leash.AttachedEntity = targetEntity;
        leash.JointId = $"leash_{leashUid}_{targetEntity}";

        var leashedComp = EnsureComp<LeashedComponent>(targetEntity);
        leashedComp.Leash = leashUid;

        // Will whine if the leash is attached
        _popupSystem.PopupClient(Loc.GetString("leash-popup-attached"), userUid, userUid);

        return true;
    }

    /// <summary>
    /// Removing the leash
    /// </summary>
    public virtual bool TryDetachLeash(EntityUid leashUid, LeashComponent? leash = null, EntityUid? user = null)
    {
        if (!Resolve(leashUid, ref leash))
            return false;

        if (leash.AttachedEntity is not { } target)
            return false;

        leash.JointId = null;
        leash.AttachedEntity = null;

        // Removing the LeashedComponent from the attached entity when the connection is broken
        if (LifeStage(target) < EntityLifeStage.Terminating)
        {
            RemCompDeferred<LeashedComponent>(target);
        }

        if (user != null)
            _popupSystem.PopupPredicted(Loc.GetString("leash-popup-detached"), user.Value, user.Value);

        return true;
    }
}
