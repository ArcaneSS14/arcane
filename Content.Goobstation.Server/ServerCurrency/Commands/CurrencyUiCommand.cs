// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.CCVar;
using Content.Goobstation.Server.ServerCurrency.UI;
using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Goobstation.Server.ServerCurrency.Commands
{
    // [AnyCommand] // Arcane: token shop hidden from players
    [AdminCommand(AdminFlags.Host)] // Arcane
    public sealed class CurrencyUiCommand : IConsoleCommand
    {
        [Dependency] private readonly IConfigurationManager _cfg = default!; // Arcane

        public string Command => "balanceui";

        public string Description => "Open the currency UI";

        public string Help => $"{Command}";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            // Arcane-start
            if (!_cfg.GetCVar(GoobCVars.ServerCurrencyEnabled))
                return;
            // Arcane-end

            var player = shell.Player;
            if (player == null)
            {
                shell.WriteLine("This does not work from the server console.");
                return;
            }

            var eui = IoCManager.Resolve<EuiManager>();
            var ui = new CurrencyEui();
            eui.OpenEui(ui, player);
        }
    }
}
