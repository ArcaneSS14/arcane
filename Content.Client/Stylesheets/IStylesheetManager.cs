// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using Robust.Client.UserInterface;
using Content.Client._Arcane.StyleSheets;

namespace Content.Client.Stylesheets;

public interface IStylesheetManager
{
    // Arcane-Start
    string CurrentTheme { get; }
    event Action? ThemeChanged;
    IReadOnlyList<ArcaneTheme> GetAvailableThemes();
    bool TrySetTheme(string themeId);
    void RefreshTheme();
    // Arcane-End
    /// Nanotrasen styles: the default style! Use this for most UIs
    Stylesheet SheetNanotrasen { get; }

    ///
    /// System styles: use this for any admin / debug menus, and any odds and ends (like the changelog for some reason)
    ///
    Stylesheet SheetSystem { get; }


    [Obsolete("Update to use SheetNanotrasen instead")]
    Stylesheet SheetNano { get; }

    [Obsolete("Update to use SheetSystem instead")]
    Stylesheet SheetSpace { get; }

    /// get a stylesheet by name
    public bool TryGetStylesheet(string name, [MaybeNullWhen(false)]  out Stylesheet stylesheet);

    void Initialize();

    ///
    /// Sheetlets marked with CommonSheetlet that have not satisfied the type constraints of any stylesheet
    ///
    public HashSet<Type> UnusedSheetlets { get; }
}
