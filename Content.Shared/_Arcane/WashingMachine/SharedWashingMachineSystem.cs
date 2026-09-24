// SPDX-FileCopyrightText: 2025 Doctor-Cpu <77215380+Doctor-Cpu@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GabyChangelog <agentepanela2@gmail.com>
// SPDX-FileCopyrightText: 2025 Will-Oliver-Br <164823659+Will-Oliver-Br@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 YaraaraY <158123176+YaraaraY@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.ActionBlocker;
using Content.Shared.Body.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Destructible;
using Content.Shared.DragDrop;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Standing;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared._Arcane.WashingMachine.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;
using System.Numerics;

namespace Content.Shared._Arcane.WashingMachine;

public abstract partial class SharedWashingMachineSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedEntityStorageSystem _storage = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;

    private static readonly TimeSpan EscapeTime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan EnterTime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StuffTime = TimeSpan.FromSeconds(3);

    private static readonly ProtoId<SpeciesPrototype>[] BigRaceSpecies =
    {
        "Oni",
        "Yowie",
    };

    protected static readonly Vector2[] CardinalOffsets =
    {
        new(0, -1),
        new(-1, 0),
        new(1, 0),
        new(0, 1),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WashingMachineComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<WashingMachineComponent, ComponentRemove>(OnRemoved);

        SubscribeLocalEvent<WashingMachineComponent, BreakageEventArgs>(OnBreak);
        SubscribeLocalEvent<WashingMachineComponent, StorageOpenAttemptEvent>(OnStorageOpenAttempt);
        SubscribeLocalEvent<WashingMachineComponent, StorageCloseAttemptEvent>(OnStorageCloseAttempt);

        SubscribeLocalEvent<WashingMachineComponent, ActivateInWorldEvent>(OnActivateInWorld, before: [typeof(SharedEntityStorageSystem)]);
        SubscribeLocalEvent<WashingMachineComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
        SubscribeLocalEvent<WashingMachineComponent, CanDropTargetEvent>(OnCanDropTarget);
        SubscribeLocalEvent<WashingMachineComponent, DragDropTargetEvent>(OnDragDropTarget);
        SubscribeLocalEvent<WashingMachineComponent, StuffInWashingMachineDoAfterEvent>(OnStuffInDoAfter);
        SubscribeLocalEvent<WashingMachineComponent, EnterWashingMachineDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<WashingMachineComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);

        SubscribeLocalEvent<WashingMachineStuckComponent, UpdateCanMoveEvent>(OnStuckMoveAttempt);
        SubscribeLocalEvent<WashingMachineStuckComponent, MoveInputEvent>(OnStuckMoveInput);
        SubscribeLocalEvent<WashingMachineStuckComponent, ComponentRemove>(OnStuckRemoved);
        SubscribeLocalEvent<WashingMachineStuckComponent, EscapeWashingMachineDoAfterEvent>(OnEscapeDoAfter);
        SubscribeLocalEvent<WashingMachineStuckComponent, MobStateChangedEvent>(OnStuckMobStateChanged);
        SubscribeLocalEvent<WashingMachineStuckComponent, DownAttemptEvent>(OnStuckDownAttempt);
        SubscribeLocalEvent<WashingMachineStuckComponent, BuckleAttemptEvent>(OnStuckBuckleAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WashingMachineActiveComponent, WashingMachineComponent>();
        while (query.MoveNext(out var uid, out var _, out var component))
        {
            if (component.WashingFinished > _timing.CurTime)
                continue;

            if (!_net.IsServer)
                continue;

            FinishWashing(uid, component);
        }

        if (!_net.IsServer)
            return;

        var stuckQuery = EntityQueryEnumerator<WashingMachineStuckComponent>();
        while (stuckQuery.MoveNext(out var uid, out var stuck))
        {
            var machine = stuck.Machine;

            if (machine is not { } machineUid || !Exists(machineUid))
            {
                EjectPlayer((uid, stuck));
                continue;
            }

            var targetPos = _transform.GetMapCoordinates(uid);
            var machinePos = _transform.GetMapCoordinates(machineUid);

            if (targetPos.MapId != machinePos.MapId
                || (targetPos.Position - machinePos.Position).LengthSquared() >= 0.01f)
                EjectPlayer((uid, stuck));
        }
    }

    private void FinishWashing(EntityUid uid, WashingMachineComponent component)
    {
        RemComp<WashingMachineActiveComponent>(uid);

        component.WashingMachineState = WashingMachineState.Idle;
        DirtyField(uid, component, nameof(WashingMachineComponent.WashingMachineState));
        _appearance.SetData(uid, WashingMachineVisuals.State, component.WashingMachineState);

        var items = new HashSet<EntityUid>();

        if (TryComp<EntityStorageComponent>(uid, out var entityStorageComp))
            items = entityStorageComp.Contents.ContainedEntities.ToHashSet();

        component.WashingSoundStream = _audio.Stop(component.WashingSoundStream);

        _audio.PlayPvs(component.FinishedSound, uid);

        var machineEv = new WashingMachineFinishedWashingEvent(items);
        RaiseLocalEvent(uid, machineEv);

        var itemEv = new WashingMachineWashedEvent(uid, items);
        foreach (var item in items)
            RaiseLocalEvent(item, itemEv);

        // update again incase forensics changed
        // such as dyeing
        UpdateForensics((uid, component), items);

        _storage.OpenStorage(uid);
    }

    private void OnInit(Entity<WashingMachineComponent> ent, ref ComponentInit args)
    {
        _appearance.SetData(ent.Owner, WashingMachineVisuals.State, ent.Comp.WashingMachineState);
    }

    private void OnRemoved(Entity<WashingMachineComponent> ent, ref ComponentRemove args)
    {
        _audio.Stop(ent.Comp.WashingSoundStream);
        EjectAllStuck(ent.Owner);
    }

    private void OnBreak(Entity<WashingMachineComponent> ent, ref BreakageEventArgs args)
    {
        ent.Comp.WashingMachineState = WashingMachineState.Broken;
        DirtyField(ent.Owner, ent.Comp, nameof(WashingMachineComponent.WashingMachineState));
        _appearance.SetData(ent.Owner, WashingMachineVisuals.State, ent.Comp.WashingMachineState);

        ent.Comp.WashingSoundStream = _audio.Stop(ent.Comp.WashingSoundStream);
        RemComp<WashingMachineActiveComponent>(ent.Owner);
        EjectAllStuck(ent.Owner);
    }

    private void OnStorageCloseAttempt(Entity<WashingMachineComponent> ent, ref StorageCloseAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = HasPersonInside(ent.Owner);
    }

    private void OnAnchorStateChanged(Entity<WashingMachineComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            return;

        EjectAllStuck(ent.Owner);
    }

    private void OnStorageOpenAttempt(Entity<WashingMachineComponent> ent, ref StorageOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = ent.Comp.WashingMachineState != WashingMachineState.Idle;
    }

    private void OnActivateInWorld(Entity<WashingMachineComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (!TryActivate(ent))
            return;

        args.Handled = true;
    }

    private void OnGetVerbs(Entity<WashingMachineComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanInteract || !args.CanComplexInteract)
            return;

        var user = args.User;

        if (CanActivate(ent))
        {
            var verb = new ActivationVerb()
            {
                Text = Loc.GetString("washing-machine-start"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/Spare/poweronoff.svg.192dpi.png")),
                Act = () => TryActivate(ent)
            };

            args.Verbs.Add(verb);
        }

        if (CanClimbIn(ent, user))
        {
            var verb = new ActivationVerb()
            {
                Text = Loc.GetString("washing-machine-climb-verb"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/in.svg.192dpi.png")),
                Act = () => TryClimbIn(ent, user)
            };

            args.Verbs.Add(verb);
        }
    }

    private bool TryActivate(Entity<WashingMachineComponent> ent)
    {
        if (!CanActivate(ent))
            return false;

        Activate(ent);
        return true;
    }

    private bool CanActivate(Entity<WashingMachineComponent> ent)
    {
        if (ent.Comp.WashingMachineState != WashingMachineState.Idle)
            return false;

        if (!_power.IsPowered(ent.Owner))
            return false;

        if (_storage.IsOpen(ent.Owner))
            return false;

        return true;
    }

    private void Activate(Entity<WashingMachineComponent> ent)
    {
        ent.Comp.WashingFinished = _timing.CurTime + ent.Comp.WashingTime;
        DirtyField(ent.Owner, ent.Comp, nameof(WashingMachineComponent.WashingFinished));

        ent.Comp.WashingMachineState = WashingMachineState.Washing;
        DirtyField(ent.Owner, ent.Comp, nameof(WashingMachineComponent.WashingMachineState));
        _appearance.SetData(ent.Owner, WashingMachineVisuals.State, ent.Comp.WashingMachineState);

        EnsureComp<WashingMachineActiveComponent>(ent.Owner);

        var items = new HashSet<EntityUid>();

        if (TryComp<EntityStorageComponent>(ent.Owner, out var entityStorageComp))
            items = entityStorageComp.Contents.ContainedEntities.ToHashSet();

        if (_net.IsServer)
        {
            var audio = _audio.PlayPvs(ent.Comp.WashingSound, ent.Owner);
            ent.Comp.WashingSoundStream = audio?.Entity;
        }

        var machineEv = new WashingMachineStartedWashingEvent(items);
        RaiseLocalEvent(ent.Owner, machineEv);

        UpdateForensics(ent, items);

        var itemEv = new WashingMachineIsBeingWashed(ent.Owner, items);
        foreach (var item in items)
            RaiseLocalEvent(item, itemEv);
    }

    protected virtual void UpdateForensics(Entity<WashingMachineComponent> ent, HashSet<EntityUid> items)
    {
    }

    private bool CanClimbIn(Entity<WashingMachineComponent> ent, EntityUid user)
    {
        if (!HasComp<StandingStateComponent>(user))
            return false;

        if (IsBigRace(user))
            return false;

        if (HasComp<WashingMachineStuckComponent>(user))
            return false;

        if (ent.Comp.WashingMachineState != WashingMachineState.Idle)
            return false;

        if (HasPersonInside(ent.Owner))
            return false;

        if (!_storage.IsOpen(ent.Owner))
            return false;

        if (_containers.IsEntityInContainer(user))
            return false;

        return true;
    }

    private bool TryClimbIn(Entity<WashingMachineComponent> ent, EntityUid user)
    {
        if (!CanClimbIn(ent, user))
            return false;

        if (!_net.IsServer)
            return false;

        var doAfter = new DoAfterArgs(EntityManager, user, EnterTime,
            new EnterWashingMachineDoAfterEvent(), ent.Owner, target: user, used: ent.Owner)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
            DuplicateCondition = DuplicateConditions.SameTool | DuplicateConditions.SameTarget
        };

        return _doAfter.TryStartDoAfter(doAfter, out _);
    }

    private void OnEnterDoAfter(Entity<WashingMachineComponent> ent, ref EnterWashingMachineDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target == null)
            return;

        var user = args.Args.Target.Value;

        if (!_net.IsServer)
            return;

        if (Deleted(user) || !CanClimbIn(ent, user))
            return;

        args.Handled = true;

        ClimbIn(ent, user);
    }

    private void OnStuckDownAttempt(Entity<WashingMachineStuckComponent> ent, ref DownAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnStuckBuckleAttempt(Entity<WashingMachineStuckComponent> ent, ref BuckleAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void ClimbIn(Entity<WashingMachineComponent> ent, EntityUid user)
    {
        InsertIntoMachine(ent, user);

        _popup.PopupPredicted(
            Loc.GetString("washing-machine-climb-self", ("machine", ent.Owner)),
            Loc.GetString("washing-machine-climb-others", ("user", Identity.Entity(user, EntityManager)), ("machine", ent.Owner)),
            user,
            user);
    }

    private void InsertIntoMachine(Entity<WashingMachineComponent> ent, EntityUid target)
    {
        if (HasPersonInside(ent.Owner))
            return;

        if (TryComp<PullableComponent>(target, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(target, pullable);

        _physics.SetCanCollide(target, false);
        _transform.SetCoordinates(target, Transform(ent.Owner).Coordinates);

        var stuck = EnsureComp<WashingMachineStuckComponent>(target);
        stuck.Machine = ent.Owner;
        Dirty(target, stuck);

        stuck.TopVisual = Spawn("WashingMachineTopVisual", new EntityCoordinates(ent.Owner, Vector2.Zero));

        _actionBlocker.UpdateCanMove(target);
    }

    private void OnCanDropTarget(Entity<WashingMachineComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        if (CanStuffIn(ent, args.Dragged, requireOpen: false))
        {
            args.Handled = true;
            args.CanDrop = true;
        }
    }

    private void OnDragDropTarget(Entity<WashingMachineComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.WashingMachineState == WashingMachineState.Idle && !_storage.IsOpen(ent.Owner))
            _storage.OpenStorage(ent.Owner);

        if (args.User == args.Dragged || !CanStuffIn(ent, args.Dragged))
            return;

        args.Handled = true;

        if (!_net.IsServer)
            return;

        var doAfter = new DoAfterArgs(EntityManager, args.User, StuffTime,
            new StuffInWashingMachineDoAfterEvent(), ent.Owner, target: args.Dragged, used: ent.Owner)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
            DuplicateCondition = DuplicateConditions.SameTool | DuplicateConditions.SameTarget
        };

        _doAfter.TryStartDoAfter(doAfter, out _);
    }

    private void OnStuffInDoAfter(Entity<WashingMachineComponent> ent, ref StuffInWashingMachineDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target == null)
            return;

        var target = args.Args.Target.Value;

        if (!_net.IsServer)
            return;

        if (Deleted(target) || !CanStuffIn(ent, target))
            return;

        InsertIntoMachine(ent, target);

        _popup.PopupPredicted(
            Loc.GetString("washing-machine-stuff-self", ("machine", ent.Owner)),
            Loc.GetString("washing-machine-stuff-others",
                ("user", Identity.Entity(args.User, EntityManager)),
                ("target", Identity.Entity(target, EntityManager)),
                ("machine", ent.Owner)),
            target,
            args.User);
    }

    private bool CanStuffIn(Entity<WashingMachineComponent> ent, EntityUid target, bool requireOpen = true)
    {
        if (!HasComp<BodyComponent>(target))
            return false;

        if (IsBigRace(target))
            return false;

        if (HasComp<WashingMachineStuckComponent>(target))
            return false;

        if (ent.Comp.WashingMachineState != WashingMachineState.Idle)
            return false;

        if (HasPersonInside(ent.Owner))
            return false;

        if (requireOpen && !_storage.IsOpen(ent.Owner))
            return false;

        if (_containers.IsEntityInContainer(target))
            return false;

        return true;
    }

    private bool IsBigRace(EntityUid uid)
    {
        return TryComp<HumanoidAppearanceComponent>(uid, out var humanoid) && BigRaceSpecies.Contains(humanoid.Species);
    }

    private bool HasPersonInside(EntityUid machine)
    {
        var query = EntityQueryEnumerator<WashingMachineStuckComponent>();
        while (query.MoveNext(out var _, out var stuck))
        {
            if (stuck.Machine == machine)
                return true;
        }

        return false;
    }

    private void OnStuckMoveAttempt(Entity<WashingMachineStuckComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnStuckMoveInput(Entity<WashingMachineStuckComponent> ent, ref MoveInputEvent args)
    {
        if (args.Dir == Direction.Invalid || !args.State)
            return;

        if (ent.Comp.EscapeDoAfter != null)
            return;

        var doAfter = new DoAfterArgs(EntityManager, ent.Owner, EscapeTime, new EscapeWashingMachineDoAfterEvent(), ent.Owner)
        {
            BreakOnDamage = true,
            BreakOnMove = false,
            DuplicateCondition = DuplicateConditions.SameTool | DuplicateConditions.SameTarget
        };

        if (_doAfter.TryStartDoAfter(doAfter, out var id))
            ent.Comp.EscapeDoAfter = id;
    }

    private void OnEscapeDoAfter(Entity<WashingMachineStuckComponent> ent, ref EscapeWashingMachineDoAfterEvent args)
    {
        ent.Comp.EscapeDoAfter = null;

        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (_net.IsServer)
            EjectPlayer(ent);
    }

    private void OnStuckRemoved(Entity<WashingMachineStuckComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.EscapeDoAfter != null)
        {
            _doAfter.Cancel(ent.Comp.EscapeDoAfter);
            ent.Comp.EscapeDoAfter = null;
        }

        if (_net.IsServer && ent.Comp.TopVisual is { } visual && CanDeleteEntity(visual))
            Del(visual);
        ent.Comp.TopVisual = null;

        _physics.SetCanCollide(ent.Owner, true);
        _actionBlocker.UpdateCanMove(ent.Owner);

        RestoreStuckVisuals(ent);
    }

    protected virtual void RestoreStuckVisuals(Entity<WashingMachineStuckComponent> ent)
    {
    }

    private void OnStuckMobStateChanged(Entity<WashingMachineStuckComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (_net.IsServer)
            EjectPlayer(ent);
    }

    private void EjectAllStuck(EntityUid machine)
    {
        if (!_net.IsServer)
            return;

        var toEject = new List<EntityUid>();
        var query = EntityQueryEnumerator<WashingMachineStuckComponent>();
        while (query.MoveNext(out var uid, out var stuck))
        {
            if (stuck.Machine == machine)
                toEject.Add(uid);
        }

        foreach (var uid in toEject)
        {
            if (!TryComp<WashingMachineStuckComponent>(uid, out var stuck))
                continue;

            EjectPlayer((uid, stuck));
        }
    }

    protected virtual void EjectPlayer(Entity<WashingMachineStuckComponent> ent)
    {
    }

    protected bool CanDeleteEntity(EntityUid uid)
    {
        return TryComp(uid, out MetaDataComponent? meta) && meta.EntityLifeStage < EntityLifeStage.Terminating;
    }
}
