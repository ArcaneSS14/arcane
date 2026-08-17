// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Singularity;
using Content.Server.Popups;
using Content.Server.Singularity.Events;
using Content.Shared.Shuttles.Components;
using Content.Shared.Popups;
using Content.Shared.Singularity.Components;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server.Singularity.EntitySystems;

public sealed class ContainmentFieldSystem : EntitySystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContainmentFieldComponent, StartCollideEvent>(HandleFieldCollide);
        SubscribeLocalEvent<ContainmentFieldComponent, EventHorizonAttemptConsumeEntityEvent>(HandleEventHorizon);
    }

    private void HandleFieldCollide(EntityUid uid, ContainmentFieldComponent component, ref StartCollideEvent args)
    {
        var otherBody = args.OtherEntity;

        // Goobstation
        var ev = new ContainmentFieldThrowEvent(uid);
        RaiseLocalEvent(otherBody, ref ev);
        if (ev.Cancelled)
            return;

        if (component.DestroyGarbage && HasComp<SpaceGarbageComponent>(otherBody))
        {
            _popupSystem.PopupEntity(Loc.GetString("comp-field-vaporized", ("entity", otherBody)), uid, PopupType.LargeCaution);
            QueueDel(otherBody);
        }

        if (TryComp<PhysicsComponent>(otherBody, out var physics) && physics.Hard) // Goobstation - removed mass check
        {
            var fieldDir = _transformSystem.GetWorldPosition(uid);
            var playerDir = _transformSystem.GetWorldPosition(otherBody);

            _throwing.TryThrow(otherBody, playerDir-fieldDir, baseThrowSpeed: component.ThrowForce);
        }
    }

private void HandleEventHorizon(EntityUid uid, ContainmentFieldComponent component, ref EventHorizonAttemptConsumeEntityEvent args)
{
    if (args.Cancelled) /// Arcane-Start
        return;

    if (args.EventHorizon.SuppressFieldConsumption)
    {
        args.Cancelled = true;
        return;
    }

    var singularityUid = args.EventHorizon.Owner;
    var singularityXform = Transform(singularityUid);
    var fieldXform = Transform(uid);

    var singularityPos = _transformSystem.GetWorldPosition(singularityXform);
    var fieldPos = _transformSystem.GetWorldPosition(fieldXform);
    var horizonRadius = args.EventHorizon.Radius;

    var distance = (fieldPos - singularityPos).Length(); /// Arcane-End

    if (distance > horizonRadius)
        args.Cancelled = true;
 // if (!args.Cancelled && !args.EventHorizon.CanBreachContainment) Arcane-Edit-Start: Removed
    /// args.Cancelled = true; Arcane-Edit-End: Removed
}
