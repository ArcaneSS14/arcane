using System.Numerics;
using Content.Goobstation.Common.CCVar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Input;

namespace Content.Client._Arcane.UserInterface.Systems.Actions.Controls;

/// <summary>
/// Grip strip above the actions bar. Dragging it moves the whole bar, right click resets the bar to its default spot.
/// It is separate from the action buttons so it never interferes with their click or drag-reorder handling.
/// </summary>
public sealed class ActionsBarDragHandle : Control
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private static readonly Color BackgroundColor = Color.FromHex("#25252a").WithAlpha(0.6f);
    private static readonly Color DotColor = Color.FromHex("#9fa2ab");

    private const float DotSize = 2f;
    private const float DotSpacing = 4f;
    private const int DotColumns = 6;
    private const int DotRows = 2;

    private bool _dragging;

    /// <summary>
    /// Raised with the global mouse position when a drag starts.
    /// </summary>
    public event Action<Vector2>? DragStarted;

    /// <summary>
    /// Raised with the global mouse position while dragging.
    /// </summary>
    public event Action<Vector2>? Dragged;

    public event Action? DragFinished;

    public event Action? ResetRequested;

    public ActionsBarDragHandle()
    {
        IoCManager.InjectDependencies(this);

        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Move;
        HorizontalExpand = true;
        MinSize = new Vector2(32, 10);
        Margin = new Thickness(0, 0, 0, 2);
        ToolTip = Loc.GetString("arcane-actions-bar-drag-handle-tooltip");
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _cfg.OnValueChanged(GoobCVars.LockActionBarDrag, OnLockChanged, true);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _cfg.UnsubValueChanged(GoobCVars.LockActionBarDrag, OnLockChanged);
        _dragging = false;
    }

    private void OnLockChanged(bool locked)
    {
        Visible = !locked;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _dragging = true;
            DragStarted?.Invoke(GlobalPosition + args.RelativePosition);
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            ResetRequested?.Invoke();
            args.Handle();
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick || !_dragging)
            return;

        _dragging = false;
        DragFinished?.Invoke();
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (_dragging)
            Dragged?.Invoke(args.GlobalPosition);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        handle.DrawRect(PixelSizeBox, BackgroundColor);

        var dot = DotSize * UIScale;
        var spacing = DotSpacing * UIScale;
        var gripSize = new Vector2(DotColumns * spacing - (spacing - dot), DotRows * spacing - (spacing - dot));
        var origin = (PixelSize - gripSize) / 2f;

        for (var x = 0; x < DotColumns; x++)
        {
            for (var y = 0; y < DotRows; y++)
            {
                var topLeft = origin + new Vector2(x * spacing, y * spacing);
                handle.DrawRect(UIBox2.FromDimensions(topLeft, new Vector2(dot, dot)), DotColor);
            }
        }
    }
}
