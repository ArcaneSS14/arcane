// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Client._Arcane.Guidebook;
using Content.Client.Guidebook.Controls;
using Robust.Shared.ContentPack;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Guidebook;

public sealed partial class GuidebookUIController
{
    [Dependency] private readonly IResourceManager _guidebookResources = default!;
    private GuidebookPreferences? _guidebookPreferences;
    private readonly List<GuidebookWindow> _additionalGuideWindows = new();
    private readonly List<OSWindow> _popOutGuideWindows = new();

    private void ConfigureWorkspaceWindow(GuidebookWindow window, bool main)
    {
        _guidebookPreferences ??= new GuidebookPreferences(_guidebookResources);
        window.ConfigureWorkspace(_guidebookPreferences, main);
        window.OnNewWindow += OnNewGuidebookWindow;
        window.OnPopOut += OnPopOutGuidebook;
        if (main && _lastEntry == null && _guidebookPreferences.Window.Entry is { } entry)
            _lastEntry = entry;
    }

    private void OnNewGuidebookWindow(GuidebookWindow source)
    {
        var window = UIManager.CreateWindow<GuidebookWindow>();
        ConfigureWorkspaceWindow(window, false);
        source.CopyWorkspaceTo(window);
        _additionalGuideWindows.Add(window);
        window.OnClose += () =>
        {
            _additionalGuideWindows.Remove(window);
            window.Dispose();
        };
        window.Open();
        // Offset successive windows so their titles and drag handles remain accessible.
        var position = source.Position + new Vector2(32, 32);
        var max = Vector2.Max(Vector2.Zero, UIManager.WindowRoot.Size - window.SetSize);
        LayoutContainer.SetPosition(window, Vector2.Clamp(position, Vector2.Zero, max));
        window.MoveToFront();
    }

    private void OnPopOutGuidebook(GuidebookWindow source)
    {
        var guide = UIManager.CreateWindow<GuidebookWindow>();
        ConfigureWorkspaceWindow(guide, false);
        source.CopyWorkspaceTo(guide);
        var window = guide.OpenPopOut();
        _popOutGuideWindows.Add(window);
        window.Closed += () => _popOutGuideWindows.Remove(window);
        source.Close();
    }

    private void CloseWorkspaceWindows()
    {
        _guideWindow?.SaveWorkspace();
        foreach (var window in _popOutGuideWindows.ToArray())
            window.Close();
        foreach (var window in _additionalGuideWindows.ToArray())
            window.Dispose();
        _additionalGuideWindows.Clear();
    }
}
