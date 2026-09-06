// SPDX-FileCopyrightText: 2025 Doctor-Cpu <77215380+Doctor-Cpu@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GabyChangelog <agentepanela2@gmail.com>
// SPDX-FileCopyrightText: 2025 Will-Oliver-Br <164823659+Will-Oliver-Br@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Forensics;
using Content.Shared._Arcane.WashingMachine;
using Content.Shared.Forensics.Components;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
// Arcane-Start

// Arcane-End

namespace Content.Server._Arcane.WashingMachine;

public sealed partial class WashingMachineSystem : SharedWashingMachineSystem
{
    // Arcane-Start
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    // Arcane-End
    public override void Initialize()
    {
        SubscribeLocalEvent<Shared._Arcane.WashingMachine.WashingMachineComponent, GetVerbsEvent<AlternativeVerb>>(AddAltVerbs);
        base.Initialize();
    }

    protected override void UpdateForensics(Entity<Shared._Arcane.WashingMachine.WashingMachineComponent> ent, HashSet<EntityUid> items)
    {
        if (!TryComp<ForensicsComponent>(ent.Owner, out var forensics))
            return;

        foreach (var item in items)
        {
            if (!TryComp<FiberComponent>(item, out var fiber))
                continue;

            var fiberText = fiber.FiberColor == null
                ? Loc.GetString("forensic-fibers", ("material", fiber.FiberMaterial))
                : Loc.GetString("forensic-fibers-colored",
                    ("color", fiber.FiberColor),
                    ("material", fiber.FiberMaterial));

            forensics.Fibers.Add(fiberText);
        }
    }
    // Arcane-Start
    private void AddAltVerbs(Entity<Shared._Arcane.WashingMachine.WashingMachineComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var target = ent;
        var user = args.User;
        var netEntity = GetNetEntity(ent);

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("washing-machine-empty"),
            IconEntity = netEntity,
            Act = () => EmptyWashingMachine(target, user)
        });
    }

    private void EmptyWashingMachine(Entity<Shared._Arcane.WashingMachine.WashingMachineComponent> ent, EntityUid user)
    {
        if (!TryComp<StorageComponent>(ent, out var storage))
            return;

        _container.EmptyContainer(storage.Container);
        _popup.PopupClient(Loc.GetString("washing-machine-emptied"), ent, user);
    }
    // Arcane-End
}
