// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Goobstation.Shared.Teleportation.Systems;
using Content.Server.Administration.Logs;
using Content.Server.Stack;
using Content.Shared.Database;
using Content.Shared.Interaction.Events;
using Content.Shared.Stacks;
using Content.Shared.Teleportation;
// Arcane	
using Content.Shared.Timing;
namespace Content.Goobstation.Server.Teleportation.Systems;

public sealed class RandomTeleportSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _alog = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedRandomTeleportSystem _sharedRtp = default!;
    // Arcane
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RandomTeleportOnUseComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(EntityUid uid, RandomTeleportOnUseComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;
        // Arcane-Start 
        if (_useDelay.IsDelayed(uid))
            return;
        // Arcane-Start-End
        if (!_sharedRtp.RandomTeleport(args.User, component, out var wp))
            return;
        // Arcane-Start
        args.Handled = true;
        _useDelay.TryResetDelay(uid);
        // Arcane-Start-End
        if (component.ConsumeOnUse)
        {
            if (TryComp<StackComponent>(uid, out var stack))
            {
                _stack.SetCount(uid, stack.Count - 1, stack);
                return;
            }
            // It's consumed on use and it's not a stack so delete it
            QueueDel(uid);
        }
        _alog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):actor} randomly teleported to {wp!} using {ToPrettyString(uid)}");
    }
}
