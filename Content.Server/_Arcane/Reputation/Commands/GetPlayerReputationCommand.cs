// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._Arcane.Reputation.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class GetPlayerReputationCommand : IConsoleCommand
{
    public string Command => "getreput";
    public string Description => "Get player's reputation value.";
    public string Help => "Usage: getreput {ckey}";
    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var playerManager = IoCManager.Resolve<IPlayerManager>();
        var repManager = IoCManager.Resolve<IEntityManager>().System<ReputationManager>();

        if (args.Length < 1)
        {
            shell.WriteLine($"Not enough arguments.\n{Help}");
            return;
        }

        if (!playerManager.TryGetPlayerDataByUsername(args[0], out var playerData))
        {
            shell.WriteLine($"Couldn't find player: {args[0]}.");
            return;
        }

        var uid = playerData.UserId;

        var value = await repManager.GetPlayerReputation(uid);

        if (value == null)
        {
            shell.WriteLine("Couldn't get player's reputation.");
            return;
        }

        shell.WriteLine($"Reputation of {args[0]}: {value}");
    }
}
