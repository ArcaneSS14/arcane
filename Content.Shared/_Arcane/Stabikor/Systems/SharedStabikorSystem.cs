using Content.Shared._Arcane.Stabikor.Components;
using Robust.Shared.Containers;
using Content.Shared.Popups;
using Content.Shared.Examine;
using Content.Shared.Inventory.Events;
using Content.Shared.Access.Components;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using Content.Shared.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Timing;
using Robust.Shared.Network;

namespace Content.Shared._Arcane.Stabikor.Systems;

public sealed class SharedStabikorSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private readonly INetManager _netManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StabikorComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<StabikorComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<StabikorComponent, BeingUnequippedAttemptEvent>(OnBeingUnequippedAttempt);
        SubscribeLocalEvent<StabikorComponent, GetVerbsEvent<ActivationVerb>>(OnGetActivationVerbs);
    }

    private void OnBeingUnequippedAttempt(EntityUid uid, StabikorComponent comp, BeingUnequippedAttemptEvent args)
    {
        if (comp.IsLocked || comp.IsActive)
        {
            if (!_netManager.IsClient)
                _popup.PopupEntity(Loc.GetString("stabikor-unequip-blocked-active"), uid, args.Unequipee, PopupType.SmallCaution);

            args.Cancel();
        }
    }

    private void OnGotUnequipped(EntityUid uid, StabikorComponent comp, ref GotUnequippedEvent args)
    {
        if (comp.IsActive)
            comp.Timer = 0;

        if (!_netManager.IsClient)
            Dirty(uid, comp);

    }

    private void OnExamine(EntityUid uid, StabikorComponent comp, ExaminedEvent args)
    {
        var state = comp.IsLocked ? "stabikor-examine-locked" : "stabikor-examine-unlocked";

        args.PushMarkup(Loc.GetString("stabikor-examine-status-main", ("mode", Loc.GetString(state))));

        var durationTotalMinutes = (int) (comp.Duration / 60f);
        var durationHours = durationTotalMinutes / 60;
        var durationMinutes = durationTotalMinutes % 60;

        args.PushMarkup(Loc.GetString("stabikor-examine-duration-info",
            ("hours", durationHours),
            ("minutes", durationMinutes)));

        if (comp.IsLocked && comp.Timer > 0)
        {
            var remainingTotalMinutes = (int) (comp.Timer / 60f);
            var remainingHours = remainingTotalMinutes / 60;
            var remainingMinutes = remainingTotalMinutes % 60;
            var remainingSeconds = (int) (comp.Timer % 60f);

            args.PushMarkup(Loc.GetString("stabikor-examine-timer-remaining",
                ("hours", remainingHours),
                ("minutes", remainingMinutes),
                ("seconds", remainingSeconds)));
        }
        Dirty(uid, comp);
    }

    private void OnGetActivationVerbs(EntityUid uid, StabikorComponent comp, GetVerbsEvent<ActivationVerb> args)
    {
        if (!_containerSystem.TryGetContainingContainer((uid, null, null), out var container))
            return;

        var parent = container.Owner;
        var isInContainer = parent.IsValid();

        if (!isInContainer && (!args.CanAccess || !args.CanInteract))
            return;

        if (!_handsSystem.TryGetActiveItem(args.User, out var heldItem) ||
            !_idCard.TryFindIdCard(heldItem.Value, out var idCard) ||
            !TryComp<AccessComponent>(idCard.Owner, out var accessComp))
            return;

        var settingsIcon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png"));

        if (!comp.IsLocked)
        {
            args.Verbs.Add(new ActivationVerb()
            {
                Text = Loc.GetString("stabikor-verb-set-duration"),
                Icon = settingsIcon,
                Act = () => FlipOpenDialog(uid, comp)
            });
        }

        var verbText = comp.IsLocked ? "stabikor-verb-unlock" : "stabikor-verb-lock";

        args.Verbs.Add(new ActivationVerb()
        {
            Text = Loc.GetString(verbText),
            Icon = settingsIcon,
            Act = () => FlipToggleLock(uid, comp)
        });
    }

    private void FlipOpenDialog(EntityUid uid, StabikorComponent comp)
    {
        if (_gameTiming.CurTime < comp.LastVerbClickTime + TimeSpan.FromSeconds(0.4))
            return;

        comp.LastVerbClickTime = _gameTiming.CurTime;

        RaiseNetworkEvent(new OpenDialogEvent(GetNetEntity(uid)));
    }

    private void FlipToggleLock(EntityUid uid, StabikorComponent comp)
    {
        if (_gameTiming.CurTime < comp.LastVerbClickTime + TimeSpan.FromSeconds(0.4))
            return;

        comp.LastVerbClickTime = _gameTiming.CurTime;

        RaiseNetworkEvent(new ToggleLockEvent(GetNetEntity(uid)));
    }
}
