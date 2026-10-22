// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client._Arcane.StyleSheets;
using Content.Client._Arcane.DiscordRoles;
using Content.Shared._Arcane.CCVars;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Reflection;

namespace Content.Client.Stylesheets
{
    public sealed class StylesheetManager : IStylesheetManager
    {
        [Dependency] private readonly ILogManager _logManager = default!;
        [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private readonly IReflectionManager _reflection = default!;
        // Arcane-Start
        [Dependency] private readonly IConfigurationManager _configuration = default!;
        [Dependency] private readonly IPlayerManager _players = default!;
        [Dependency] private readonly DiscordRoleManager _discordRoles = default!;

        public string CurrentTheme { get; private set; } = ArcanePalette.DefaultThemeId;
        public event Action? ThemeChanged;

        public IReadOnlyList<ArcaneTheme> GetAvailableThemes()
        {
            var themes = new List<ArcaneTheme>();
            foreach (var theme in ArcanePalette.Themes)
            {
                if (CanUseTheme(theme))
                    themes.Add(theme);
            }

            return themes;
        }

        public bool TrySetTheme(string themeId)
        {
            foreach (var theme in ArcanePalette.Themes)
            {
                if (theme.Id != themeId || !CanUseTheme(theme))
                    continue;

                _configuration.SetCVar(ACCVars.UiTheme, themeId);
                RefreshTheme();
                return true;
            }

            return false;
        }

        public void RefreshTheme()
        {
            var requested = _configuration.GetCVar(ACCVars.UiTheme);
            var selected = ArcanePalette.DefaultTheme;
            foreach (var theme in ArcanePalette.Themes)
            {
                if (theme.Id == requested && CanUseTheme(theme))
                {
                    selected = theme;
                    break;
                }
            }

            if (CurrentTheme != selected.Id)
            {
                var previousNanotrasen = SheetNanotrasen;
                var previousSystem = SheetSystem;
                CurrentTheme = selected.Id;
                var stylesheet = Stylesheets[selected.Id];
                SheetNanotrasen = stylesheet;
                SheetSystem = stylesheet;
                Stylesheets["Nanotrasen"] = stylesheet;
                Stylesheets["System"] = stylesheet;
                _userInterfaceManager.Stylesheet = stylesheet;
                UpdateExplicitStylesheets(_userInterfaceManager.RootControl, previousNanotrasen, previousSystem, stylesheet);
            }

            ThemeChanged?.Invoke();
        }

        private bool CanUseTheme(ArcaneTheme theme)
        {
            if (theme.RequiredRole == null)
                return true;

            var session = _players.LocalSession;
            return session != null && _discordRoles.HasRole(session, theme.RequiredRole.Value);
        }

        private static void UpdateExplicitStylesheets(
            Control control,
            Stylesheet previousNanotrasen,
            Stylesheet previousSystem,
            Stylesheet stylesheet)
        {
            if (ReferenceEquals(control.Stylesheet, previousNanotrasen) ||
                ReferenceEquals(control.Stylesheet, previousSystem))
            {
                control.Stylesheet = stylesheet;
            }

            foreach (var child in control.Children)
                UpdateExplicitStylesheets(child, previousNanotrasen, previousSystem, stylesheet);
        }
        // Arcane-End

        [Dependency]
        private readonly IResourceCache
            _resCache = default!; // TODO: REMOVE (obsolete; used to construct StyleNano/StyleSpace)

        public Stylesheet SheetNanotrasen { get; private set; } = default!;
        public Stylesheet SheetSystem { get; private set; } = default!;

        [Obsolete("Update to use SheetNanotrasen instead")]
        public Stylesheet SheetNano { get; private set; } = default!;

        [Obsolete("Update to use SheetSystem instead")]
        public Stylesheet SheetSpace { get; private set; } = default!;

        private Dictionary<string, Stylesheet> Stylesheets { get; set; } = default!;

        public bool TryGetStylesheet(string name, [MaybeNullWhen(false)] out Stylesheet stylesheet)
        {
            return Stylesheets.TryGetValue(name, out stylesheet);
        }

        public HashSet<Type> UnusedSheetlets { get; private set; } = [];

        public void Initialize()
        {
            var sawmill = _logManager.GetSawmill("style");
            sawmill.Debug("Initializing Stylesheets...");
            var sw = Stopwatch.StartNew();

            // add all sheetlets to the hashset
            var tys = _reflection.FindTypesWithAttribute<CommonSheetletAttribute>();
            UnusedSheetlets = [..tys];

            Stylesheets = new Dictionary<string, Stylesheet>();
            // Arcane-Edit-Start
            SheetNanotrasen = Init(new ArcaneStylesheet(new BaseStylesheet.NoConfig(), this, "Nanotrasen", ArcanePalette.DefaultTheme));
            SheetSystem = Init(new ArcaneStylesheet(new BaseStylesheet.NoConfig(), this, "System", ArcanePalette.DefaultTheme));
            foreach (var theme in ArcanePalette.Themes)
            {
                if (theme == ArcanePalette.DefaultTheme)
                    Stylesheets.Add(theme.Id, SheetNanotrasen);
                else
                    Init(new ArcaneStylesheet(new BaseStylesheet.NoConfig(), this, theme.Id, theme));
            }
            // Arcane-Edit-End
            SheetNano = new StyleNano(_resCache).Stylesheet; // TODO: REMOVE (obsolete)
            SheetSpace = new StyleSpace(_resCache).Stylesheet; // TODO: REMOVE (obsolete)

            _userInterfaceManager.Stylesheet = SheetNanotrasen;
            // Arcane-Start
            _discordRoles.RolesUpdated += RefreshTheme;
            _configuration.OnValueChanged(ACCVars.UiTheme, _ => RefreshTheme());
            RefreshTheme();
            // Arcane-End

            // warn about unused sheetlets
            if (UnusedSheetlets.Count > 0)
            {
                var sheetlets = UnusedSheetlets.AsEnumerable()
                    .Take(5)
                    .Select(t => t.FullName ?? "<could not get FullName>")
                    .ToArray();
                sawmill.Error($"There are unloaded sheetlets: {string.Join(", ", sheetlets)}");
            }

            sawmill.Debug($"Initialized {_styleRuleCount} style rules in {sw.Elapsed}");
        }

        private int _styleRuleCount;

        private Stylesheet Init(BaseStylesheet baseSheet)
        {
            Stylesheets.Add(baseSheet.StylesheetName, baseSheet.Stylesheet);
            _styleRuleCount += baseSheet.Stylesheet.Rules.Count;
            return baseSheet.Stylesheet;
        }
    }
}
