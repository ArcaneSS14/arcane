using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Guidebook.Controls;

public sealed partial class GuidebookWindow
{
    private OSWindow? _popOutWindow;

    private Vector2 WorkspaceSize => _popOutWindow?.Size ?? (Size.X > 0 && Size.Y > 0 ? Size : SetSize);

    public OSWindow OpenPopOut()
    {
        var window = new OSWindow
        {
            Title = Title ?? Loc.GetString("guidebook-window-title"),
            SetSize = SetSize,
        };
        window.Show();

        var parent = ContentsContainer.Parent!;
        ContentsContainer.Orphan();

        var background = new PanelContainer();
        background.AddStyleClass("BackgroundPanel");
        background.AddChild(ContentsContainer);

        window.AddChild(background);

        _popOutWindow = window;
        WorkspaceToolbar.PopOut.Visible = false;
        RestoreWorkspaceLayout();

        window.Closed += () =>
        {
            SaveWorkspace();

            ContentsContainer.Orphan();
            parent.AddChild(ContentsContainer);

            _popOutWindow = null;
            Dispose();
            window.Dispose();
        };
        return window;
    }
}
