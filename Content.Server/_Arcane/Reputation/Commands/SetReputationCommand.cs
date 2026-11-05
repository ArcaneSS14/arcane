// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._Arcane.Reputation.Commands;

[AdminCommand(AdminFlags.Host)]
public sealed class SetReputationCommand : IConsoleCommand
{
    public string Command => "setreput";
    public string Description => "Sets the reputation to the certain value.";
    public string Help => "Usage: setrep {ckey} {value}";
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

        repManager.SetPlayerReputation(uid, value, admin);

        shell.WriteLine($"Set reputation of {args[0]} to {args[1]}.");
    }
}
