// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._Arcane.Reputation.Commands;

[AdminCommand(AdminFlags.Host)]
public sealed class ModifyReputationCommand : IConsoleCommand
{
    public string Command => "modifyreput";
    public string Description => "Add the value to user's reputation.";
    public string Help => "Usage: modifyreput {ckey} {valueToAdd}";
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var playerManager = IoCManager.Resolve<IPlayerManager>();
        var repManager = IoCManager.Resolve<IEntityManager>().System<ReputationManager>();

        if (args.Length < 2)
        {
            shell.WriteLine($"Not enough arguments.\n{Help}");
            return;
        }

        if (!playerManager.TryGetPlayerDataByUsername(args[0], out var playerData))
        {
            shell.WriteLine($"Couldn't find player: {args[0]}.");
            return;
        }

        if (!float.TryParse(args[1], out var value))
        {
            shell.WriteLine($"Invalid value: {args[1]}.");
            return;
        }

        var uid = playerData.UserId;
        var admin = playerData.UserName;

        repManager.ModifyPlayerReputation(uid, value, admin);

        shell.WriteLine($"Added {args[1]} to the reputation of {args[0]}.");
    }
}
