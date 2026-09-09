// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Content.Client._Arcane.Guidebook;
using Content.Client.UserInterface.ControlExtensions;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.Guidebook.Controls;

public sealed partial class GuidebookWindow
{
    private static readonly Regex SectionSlugRegex = new(@"[^\p{L}\p{N}]+");
    private static readonly Regex SearchMarkupRegex = new(@"<!--.*?-->|<[^>]*>|\[[^\]]*\]", RegexOptions.Singleline);
    private static readonly Regex WhitespaceRegex = new(@"\s+");

    private GuidebookPreferences? _preferences;
    private GuidebookWindowState _workspaceState = new();
    private bool _saveWorkspace;
    private string? _currentArticle;
    private string? _ruleHomeEntry;
    private string? _category;
    private readonly List<GuideEntry> _categories = new();
    private readonly Dictionary<string, HashSet<string>> _categoryEntries = new();
    private readonly Dictionary<string, Button> _categoryButtons = new();
    private readonly Dictionary<string, string> _searchText = new();
    private readonly Dictionary<string, Control> _sectionAnchors = new();
    private List<ProtoId<GuideEntryPrototype>>? _workspaceRoots;
    private ProtoId<GuideEntryPrototype>? _workspaceForcedRoot;
    private bool _searchQueued;
    private int _articleVersion;

    public event Action<GuidebookWindow>? OnNewWindow;
    public event Action<GuidebookWindow>? OnPopOut;

    private void InitializeWorkspace()
    {
        WorkspaceToolbar.NewWindow.OnPressed += _ => OnNewWindow?.Invoke(this);
        WorkspaceToolbar.PopOut.OnPressed += _ => OnPopOut?.Invoke(this);
        WorkspaceToolbar.ToggleSidebar.OnPressed += _ =>
        {
            _workspaceState.SidebarHidden = !_workspaceState.SidebarHidden;
            ApplySidebar();
        };

        WorkspaceToolbar.Favorite.OnPressed += _ =>
        {
            if (_currentArticle != null)
                _preferences?.ToggleFavorite(_currentArticle);
        };

        WorkspaceToolbar.Search.OnTextChanged += _ => QueueWorkspaceSearch();
        WorkspaceToolbar.Favorites.OnPressed += _ => RefreshWorkspaceSearch();


        Split.OnSplitResizeFinished += () =>
        {
            if (!_workspaceState.SidebarHidden)
                _workspaceState.SidebarWidth = Split.SplitCenter;
        };

        HomeButton.OnPressed += _ =>
        {
            if (_ruleHomeEntry != null && _entries.TryGetValue(_ruleHomeEntry, out var entry))
                ShowGuide(entry);
        };

        OnClose += SaveWorkspace;
        OnOpen += RestoreWorkspaceLayout;
    }

    public void ConfigureWorkspace(GuidebookPreferences preferences, bool saveWindow)
    {
        _preferences = preferences;
        _saveWorkspace = saveWindow;

        var saved = preferences.Window;

        _workspaceState = new GuidebookWindowState
        {
            Width = saved.Width,
            Height = saved.Height,
            SidebarWidth = saved.SidebarWidth,
            SidebarHidden = saved.SidebarHidden,
            Category = saved.Category,
            Entry = saved.Entry,
            Expanded = new(saved.Expanded ?? new()),
            Scroll = new(saved.Scroll ?? new()),
        };

        SetSize = new Vector2(ValidSize(saved.Width, 640, 1600), ValidSize(saved.Height, 400, 1200));
        preferences.FavoritesChanged += OnFavoritesChanged;
    }

    public void CopyWorkspaceTo(GuidebookWindow window)
    {
        RememberTreeExpansion();
        RememberArticleScroll();

        window._workspaceState.SidebarWidth = _workspaceState.SidebarWidth;
        window._workspaceState.SidebarHidden = _workspaceState.SidebarHidden;
        window._workspaceState.Category = _category;
        window._workspaceState.Expanded = new(_workspaceState.Expanded);
        window._workspaceState.Scroll = new(_workspaceState.Scroll);
        window.SetSize = WorkspaceSize;

        window.UpdateGuides(new(_entries), _workspaceRoots, _workspaceForcedRoot,
            _currentArticle == null ? (ProtoId<GuideEntryPrototype>?) null : new ProtoId<GuideEntryPrototype>(_currentArticle));
    }

    private static float ValidSize(float value, float min, float max)
    {
        return float.IsFinite(value) ? Math.Clamp(value, min, max) : min;
    }

    private void RestoreWorkspaceLayout()
    {
        ApplySidebar();
        UserInterfaceManager.DeferAction(() =>
        {
            if (Disposed)
                return;
            Split.SplitCenter = ValidSize(_workspaceState.SidebarWidth, 140, Math.Max(140, WorkspaceSize.X - 320));
        });
    }

    private void ApplySidebar()
    {
        var hidden = _workspaceState.SidebarHidden || _entries.Count == 1;

        WorkspaceToolbar.ToggleSidebar.Pressed = !hidden;
        WorkspaceToolbar.ToggleSidebar.Disabled = _entries.Count == 1;

        if (hidden && ArticleBox.Parent == Split)
        {
            Split.RemoveChild(ArticleBox);
            ContentHost.AddChild(ArticleBox);
        }
        else if (!hidden && ArticleBox.Parent == ContentHost)
        {
            ContentHost.RemoveChild(ArticleBox);
            Split.AddChild(ArticleBox);
        }

        Split.Visible = !hidden;
    }

    public void SaveWorkspace()
    {
        RememberTreeExpansion();
        RememberArticleScroll();

        _workspaceState.Width = WorkspaceSize.X;
        _workspaceState.Height = WorkspaceSize.Y;
        _workspaceState.Category = _category;
        _workspaceState.Entry = _currentArticle;

        if (_saveWorkspace)
            _preferences?.SaveWindow(_workspaceState);
    }

    private void RememberTreeExpansion()
    {
        foreach (var item in Tree.Items)
        {
            if (item.Metadata is GuideEntry entry)
                _workspaceState.Expanded[$"{_category}/{entry.Id}"] = item.Expanded;
        }
    }

    private void RememberArticleScroll()
    {
        if (_currentArticle != null)
            _workspaceState.Scroll[_currentArticle] = Scroll.VScroll;
    }

    private void UpdateWorkspaceGuides(List<ProtoId<GuideEntryPrototype>>? roots,
        ProtoId<GuideEntryPrototype>? forcedRoot, ProtoId<GuideEntryPrototype>? selected)
    {
        RememberTreeExpansion();
        RememberArticleScroll();

        _workspaceRoots = roots?.ToList();
        _workspaceForcedRoot = forcedRoot;

        _searchText.Clear();
        _categoryEntries.Clear();
        _categories.Clear();
        _categoryButtons.Clear();

        WorkspaceToolbar.Categories.DisposeAllChildren();

        var categoryRoots = forcedRoot == null ? roots : new List<ProtoId<GuideEntryPrototype>> { forcedRoot.Value };
        _categories.AddRange(GetSortedEntries(categoryRoots));

        foreach (var root in _categories)
        {
            var descendants = new HashSet<string>();
            var pending = new Stack<string>();
            pending.Push(root.Id);

            if (forcedRoot != null)
            {
                foreach (var other in GetSortedEntries(roots))
                    pending.Push(other.Id);
            }
            while (pending.TryPop(out var id))
            {
                if (!descendants.Add(id) || !_entries.TryGetValue(id, out var entry))
                    continue;
                foreach (var child in entry.Children)
                    pending.Push(child);
            }

            _categoryEntries[root.Id] = descendants;
            var button = new Button { Text = Loc.GetString(root.Name), ToggleMode = true };
            button.OnPressed += _ => SelectCategory(root.Id, true);
            WorkspaceToolbar.Categories.AddChild(button);
            _categoryButtons[root.Id] = button;
        }

        ClearSelectedGuide();
        Tree.Clear();

        var preferred = _category ?? _workspaceState.Category;
        _category = null;

        var category = preferred != null && _categoryEntries.ContainsKey(preferred)
            ? preferred : _categories.FirstOrDefault()?.Id;
        if (category != null)
            SelectCategory(category, false);

        if (selected == null && _workspaceState.Entry is { } saved && _entries.ContainsKey(saved))
            selected = saved;

        if (_entries.Count == 1)
            selected = _entries.Keys.First();

        if (selected is { } target && _entries.ContainsKey(target))
            SelectGuide(target);
        else if (category != null)
            SelectGuide(category);

        RestoreTreeExpansion();
        ApplySidebar();
        RefreshWorkspaceSearch();
    }

    private void SelectCategory(string category, bool showRoot)
    {
        if (_category != category)
        {
            RememberTreeExpansion();
            _category = category;

            RepopulateTree(_workspaceForcedRoot == null ? new() { category } : _workspaceRoots, _workspaceForcedRoot);
            Tree.SetAllExpanded(false);
            Tree.SetAllExpanded(true, 1);

            RestoreTreeExpansion();
            foreach (var (id, button) in _categoryButtons)
                button.Pressed = id == category;
        }
        if (showRoot)
            SelectGuide(category);
    }

    private void RestoreTreeExpansion()
    {
        foreach (var item in Tree.Items)
        {
            if (item.Metadata is GuideEntry entry &&
                _workspaceState.Expanded.TryGetValue($"{_category}/{entry.Id}", out var expanded))
                item.SetExpanded(expanded);
        }
    }

    private void SelectEntryCategory(GuideEntry entry)
    {
        if (_category != null && _categoryEntries[_category].Contains(entry.Id))
            return;

        foreach (var category in _categories)
        {
            if (!_categoryEntries[category.Id].Contains(entry.Id))
                continue;

            SelectCategory(category.Id, false);
            break;
        }
    }

    private void ClearWorkspaceArticle()
    {
        RememberArticleScroll();

        _currentArticle = null;
        _articleVersion++;
        _sectionAnchors.Clear();

        WorkspaceToolbar.Favorite.Disabled = true;
    }

    private void RefreshWorkspaceArticle(GuideEntry entry)
    {
        _currentArticle = entry.Id;
        var version = ++_articleVersion;
        Title = Loc.GetString("arcane-guidebook-title", ("article", Loc.GetString(entry.Name)));
        if (_popOutWindow != null)
            _popOutWindow.Title = Title;

        WorkspaceToolbar.Status.Visible = false;
        WorkspaceToolbar.Favorite.Disabled = _preferences == null;
        WorkspaceToolbar.Favorite.Pressed = _preferences?.Favorites.Contains(entry.Id) == true;
        ReturnContainer.Visible = entry.RuleEntry;
        _sectionAnchors.Clear();

        foreach (var control in Descendants(EntryContainer))
        {
            if (control is not Label { Text: { } text } label ||
                !(label.HasStyleClass("LabelHeadingBigger") || label.HasStyleClass("LabelHeading") ||
                  label.HasStyleClass("LabelKeyText")))
                continue;

            var slug = SectionSlugRegex.Replace(text.ToLowerInvariant(), "-").Trim('-');
            if (slug.Length == 0)
                slug = "section";

            var id = slug;
            for (var suffix = 2; !_sectionAnchors.TryAdd(id, control); suffix++)
                id = $"{slug}-{suffix}";
        }
        var position = _workspaceState.Scroll.GetValueOrDefault(entry.Id);
        UserInterfaceManager.DeferAction(() =>
        {
            if (!Disposed && version == _articleVersion)
                Scroll.SetScrollValue(new Vector2(0, float.IsFinite(position) ? Math.Max(0, position) : 0));
        });
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        var pending = new Stack<Control>(parent.Children.Reverse());
        while (pending.TryPop(out var control))
        {
            yield return control;
            foreach (var child in control.Children.Reverse())
                pending.Push(child);
        }
    }

    private void HandleWorkspaceLink(string link)
    {
        WorkspaceToolbar.Status.Visible = false;
        var parts = link.Split('#', 2);
        var entry = parts[0].Length == 0 ? _currentArticle : parts[0];
        if (entry == null || !_entries.ContainsKey(entry))
        {
            ShowWorkspaceLinkError();
            return;
        }

        SelectGuide(entry);
        if (parts.Length != 2)
            return;

        var anchor = parts[1];
        if (_sectionAnchors.TryGetValue(anchor, out var control))
        {
            ScrollToSection(control);
            return;
        }

        ShowWorkspaceLinkError();
    }

    private void ShowWorkspaceLinkError()
    {
        WorkspaceToolbar.Status.Text = Loc.GetString("arcane-guidebook-link-missing");
        WorkspaceToolbar.Status.Visible = true;
    }

    private void ScrollToSection(Control control)
    {
        var version = ++_articleVersion;
        UserInterfaceManager.DeferAction(() =>
        {
            if (Disposed || control.Disposed || version != _articleVersion ||
                control.GetControlScrollPosition() is not { } position)
                return;

            Scroll.SetScrollValue(position);
        });
    }

    private void QueueWorkspaceSearch()
    {
        if (_searchQueued)
            return;

        _searchQueued = true;
        UserInterfaceManager.DeferAction(() =>
        {
            _searchQueued = false;
            if (!Disposed)
                RefreshWorkspaceSearch();
        });
    }

    private void OnFavoritesChanged()
    {
        WorkspaceToolbar.Favorite.Pressed = _currentArticle != null &&
            _preferences?.Favorites.Contains(_currentArticle) == true;

        RefreshWorkspaceSearch();
    }

    private void RefreshWorkspaceSearch()
    {
        var query = WorkspaceToolbar.Search.Text.Trim();
        WorkspaceToolbar.Results.DisposeAllChildren();
        WorkspaceToolbar.ResultsScroll.Visible = query.Length > 0 || WorkspaceToolbar.Favorites.Pressed
        ;
        if (!WorkspaceToolbar.ResultsScroll.Visible)
            return;

        foreach (var entry in _entries.Values.OrderBy(e => Loc.GetString(e.Name)))
        {
            if (WorkspaceToolbar.Favorites.Pressed && _preferences?.Favorites.Contains(entry.Id) != true)
                continue;

            var title = Loc.GetString(entry.Name);
            if (query.Length > 0 && !title.Contains(query, StringComparison.CurrentCultureIgnoreCase) &&
                !GetSearchText(entry).Contains(query, StringComparison.CurrentCultureIgnoreCase))
                continue;

            var category = _categories.FirstOrDefault(c => _categoryEntries[c.Id].Contains(entry.Id));
            var caption = category == null ? title : Loc.GetString("arcane-guidebook-result",
                ("article", title), ("category", Loc.GetString(category.Name)));

            var button = new Button { Text = caption, TextAlign = Label.AlignMode.Left, ToolTip = caption };
            button.OnPressed += _ => HandleClick(entry.Id);
            WorkspaceToolbar.Results.AddChild(button);
        }

        if (WorkspaceToolbar.Results.ChildCount == 0)
            WorkspaceToolbar.Results.AddChild(new Label { Text = Loc.GetString("arcane-guidebook-no-results") });
    }

    private string GetSearchText(GuideEntry entry)
    {
        if (_searchText.TryGetValue(entry.Id, out var text))
            return text;

        try
        {
            using var reader = _resourceManager.ContentFileReadText(entry.Text);
            text = WhitespaceRegex.Replace(SearchMarkupRegex.Replace(reader.ReadToEnd(), " "), " ");
        }
        catch (System.IO.IOException e)
        {
            _sawmill.Warning($"Cannot search guide {entry.Id}: {e.Message}");
            text = "";
        }

        _searchText[entry.Id] = text;
        return text;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _preferences != null)
            _preferences.FavoritesChanged -= OnFavoritesChanged;

        base.Dispose(disposing);
    }
}
