using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Utility;
using static Content.Shared.Administration.SharedBwoinkSystem;

namespace Content.Server._Arcane.Administration.Commands;

/// <summary>
///     Sends a fake admin ahelp message to a player so the ahelp UI (unread indication, sound) can be tested.
/// </summary>
[AdminCommand(AdminFlags.Host)]
public sealed partial class TestAHelpCommand : IConsoleCommand
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IEntityManager _entManager = default!;

    public string Command => "testahelp";
    public string Description => Loc.GetString("cmd-testahelp-desc");
    public string Help => Loc.GetString("cmd-testahelp-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var target))
        {
            shell.WriteError(Loc.GetString("cmd-testahelp-player-not-found", ("player", args[0])));
            return;
        }

        var text = args.Length > 1
            ? string.Join(' ', args[1..])
            : Loc.GetString("cmd-testahelp-default-message");

        var roundId = _entManager.System<GameTicker>().RoundId;
        var sender = Loc.GetString("cmd-testahelp-sender");
        var message = new BwoinkTextMessage(
            target.UserId,
            SystemUserId,
            $"[color=red]{sender}[/color]: {FormattedMessage.EscapeText(text)}",
            roundId: roundId);

        _entManager.EntityNetManager.SendSystemNetworkMessage(message, target.Channel);
        shell.WriteLine(Loc.GetString("cmd-testahelp-sent", ("player", target.Name)));
    }
}
