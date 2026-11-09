// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Goobstation.Wizard.Components;
using Content.Shared.Access.Components;
using Content.Shared.Hands.EntitySystems; // Orion
using Content.Shared.Inventory; // Orion
using Content.Shared.Overlays;
using Content.Shared.PDA;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

public sealed class ShowJobIconsSystem : EquipmentHudSystem<ShowJobIconsComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    // Arcane-Edit-Start
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    // Arcane-Edit-End

    private static readonly ProtoId<JobIconPrototype> JobIconForNoId = "JobIconNoId";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HasJobIconsComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent); // Goob edit
    }

    private void OnGetStatusIconsEvent(EntityUid uid, HasJobIconsComponent _, ref GetStatusIconsEvent ev) // Goob edit
    {
        if (!IsActive)
            return;

        var iconId = JobIconForNoId;

        // Arcane-Edit-Start: This runs every frame for every visible mob, so don't collect the items into a new set.
        var found = false;
        foreach (var item in _hands.EnumerateHeld(uid))
        {
            if (TryGetJobIcon(item, ref iconId))
            {
                found = true;
                break;
            }
        }

        if (!found && _inventory.TryGetSlotEntity(uid, "id", out var idSlot))
            TryGetJobIcon(idSlot.Value, ref iconId);
        // Arcane-Edit-End

        if (_prototype.Resolve(iconId, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
        else
            Log.Error($"Invalid job icon prototype: {iconPrototype}");
    }

    // Arcane-Edit-Start
    private bool TryGetJobIcon(EntityUid item, ref ProtoId<JobIconPrototype> iconId)
    {
        // ID Card
        if (TryComp<IdCardComponent>(item, out var id))
        {
            iconId = id.JobIcon;
            return true;
        }

        // PDA
        if (TryComp<PdaComponent>(item, out var pda)
            && pda.ContainedId != null
            && TryComp(pda.ContainedId, out id))
        {
            iconId = id.JobIcon;
            return true;
        }

        return false;
    }
    // Arcane-Edit-End
}
