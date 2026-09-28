using System.Numerics;
using Content.Shared._Arcane.CCVars;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Input;

namespace Content.Client._Arcane.UserInterface.Systems.Actions.Controls;

/// <summary>
/// Полоска над панелью действий: перетаскивание двигает панель, ПКМ сбрасывает раскладку.
/// Видна только при включённом свободном размещении.
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

    private static readonly Color HighlightColor = Color.FromHex("#4a90d9").WithAlpha(0.85f);

    private bool _dragging;
    private bool _highlighted;

    public event Action<Vector2>? DragStarted;

    public event Action<Vector2>? Dragged;

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
        _cfg.OnValueChanged(ACCVars.ActionsBarFreePlacement, OnFreePlacementChanged, true);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _cfg.UnsubValueChanged(ACCVars.ActionsBarFreePlacement, OnFreePlacementChanged);
        _dragging = false;
    }

    public void SetHighlighted(bool highlighted)
    {
        _highlighted = highlighted;
    }

    private void OnFreePlacementChanged(bool enabled)
    {
        Visible = enabled;
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

        handle.DrawRect(PixelSizeBox, _highlighted ? HighlightColor : BackgroundColor);

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
