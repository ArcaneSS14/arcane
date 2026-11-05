// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Arcane.Reputation.Commands;

[AnyCommand]
public sealed class ShowReputationCommand : IConsoleCommand
{
    public string Command => "showreput";
    public string Description => "Узнать свою репутацию.";
    public string Help => "Использование: showreput";
    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player == null)
            return;

        var repManager = IoCManager.Resolve<IEntityManager>().System<ReputationManager>();

        var value = await repManager.GetPlayerReputation(shell.Player.UserId);
        if (value == null)
        {
            shell.WriteLine("Не удалось получить данные о репутации. Обратитесь к кодерам или попробуйте ещё раз.");
            return;
        }

        shell.WriteLine($"Ваша репутация: {value}");
    }
}
