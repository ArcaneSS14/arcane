// SPDX-License-Identifier: MIT

using Content.Server.NPC.HTN;
using Content.Shared.CombatMode;

namespace Content.Server.CombatMode;

public sealed class CombatModeSystem : SharedCombatModeSystem
{
    // Arcane-Start
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CombatModeBlockItemPickupChangedMessage>(OnBlockItemPickupChanged);
    }
    // Arcane-End

    protected override bool IsNpc(EntityUid uid)
    {
        return HasComp<HTNComponent>(uid);
    }

    // Arcane-Start
    private void OnBlockItemPickupChanged(CombatModeBlockItemPickupChangedMessage msg, EntitySessionEventArgs args)
    {
        var uid = args.SenderSession.AttachedEntity;
        if (uid == null || !TryComp<CombatModeComponent>(uid.Value, out var comp))
            return;

        comp.BlockItemPickup = msg.BlockPickup;
        Dirty(uid.Value, comp);
    }
    // Arcane-End
}
