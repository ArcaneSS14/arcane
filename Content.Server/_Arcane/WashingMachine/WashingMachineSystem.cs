// SPDX-FileCopyrightText: 2025 Doctor-Cpu <77215380+Doctor-Cpu@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GabyChangelog <agentepanela2@gmail.com>
// SPDX-FileCopyrightText: 2025 Will-Oliver-Br <164823659+Will-Oliver-Br@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Forensics;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Forensics.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared._Arcane.WashingMachine;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Arcane.WashingMachine;

public sealed partial class WashingMachineSystem : SharedWashingMachineSystem
{
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void EjectPlayer(Entity<WashingMachineStuckComponent> ent)
    {
        var uid = ent.Owner;

        if (ent.Comp.Machine is { } machine && Exists(machine))
        {
            if (TryFindEjectTile(machine, out var coords))
                _transform.SetCoordinates(uid, coords);
        }

        if (ent.Comp.TopVisual is { } visual && CanDeleteEntity(visual))
            Del(visual);

        var machineUid = ent.Comp.Machine ?? uid;
        _popup.PopupPredicted(
            Loc.GetString("washing-machine-escape-self", ("machine", machineUid)),
            Loc.GetString("washing-machine-escape-others", ("user", Identity.Entity(uid, EntityManager)), ("machine", machineUid)),
            uid,
            uid);

        RemComp<WashingMachineStuckComponent>(uid);
    }

    private bool TryFindEjectTile(EntityUid machine, out EntityCoordinates coords)
    {
        coords = default;
        var xform = Transform(machine);

        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var gridPos = xform.Coordinates.Position;

        foreach (var offset in CardinalOffsets)
        {
            var candidate = new EntityCoordinates(gridUid, gridPos + offset).SnapToGrid();

            if (!_maps.TryGetTileRef(gridUid, grid, candidate, out var tileRef))
                continue;

            if (tileRef.Tile.IsEmpty)
                continue;

            if (_turf.IsTileBlocked(tileRef, CollisionGroup.MobMask))
                continue;

            coords = candidate;
            return true;
        }

        return false;
    }

    protected override void UpdateForensics(Entity<WashingMachineComponent> ent, HashSet<EntityUid> items)
    {
        if (!TryComp<ForensicsComponent>(ent.Owner, out var forensics))
            return;

        foreach (var item in items)
        {
            if (!TryComp<FiberComponent>(item, out var fiber))
                continue;

            var fiberText = fiber.FiberColor == null
                ? Loc.GetString("forensic-fibers", ("material", fiber.FiberMaterial))
                : Loc.GetString("forensic-fibers-colored", ("color", fiber.FiberColor), ("material", fiber.FiberMaterial));

            if (forensics.Fibers.Add(fiberText))
                Dirty(ent.Owner, forensics);
        }
    }
}
