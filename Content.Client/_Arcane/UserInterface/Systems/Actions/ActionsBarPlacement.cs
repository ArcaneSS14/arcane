using System.Numerics;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Arcane.UserInterface.Systems.Actions;

/// <summary>
/// Places the actions bar inside its parent <see cref="LayoutContainer"/>: either at the screen's default spot
/// or at the player-chosen position from <see cref="ActionsBarLayoutUIController"/>, dragged with the bar's grip.
/// </summary>
public sealed class ActionsBarPlacement
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private readonly ActionsBar _bar;
    private readonly Func<Vector2> _defaultPosition;
    private readonly ActionsBarLayoutUIController _layout;

    private Vector2 _grabOffset;

    public bool IsCustom => _layout.BarPosition != null;

    /// <summary>
    /// Raised when the bar or slot layout changes.
    /// </summary>
    public event Action? LayoutChanged;

    public ActionsBarPlacement(ActionsBar bar, Func<Vector2> defaultPosition)
    {
        IoCManager.InjectDependencies(this);

        _bar = bar;
        _defaultPosition = defaultPosition;
        _layout = _ui.GetUIController<ActionsBarLayoutUIController>();

        _bar.ActionsContainer.PositionSpace = _bar.Parent;
        _bar.ActionsContainer.ArcaneLayoutChanged += OnLayoutChanged;

        _bar.DragHandle.DragStarted += OnDragStarted;
        _bar.DragHandle.Dragged += OnDragged;
        _bar.DragHandle.ResetRequested += _layout.Reset;

        _bar.OnResized += UpdateLayout;
        if (_bar.Parent != null)
            _bar.Parent.OnResized += UpdateLayout;
    }

    /// <summary>
    /// Re-applies the current position. Call it when anything the default position depends on changes size.
    /// </summary>
    public void UpdateLayout()
    {
        SetPosition(_layout.BarPosition is { } custom ? Clamp(custom) : _defaultPosition());
    }

    private void OnLayoutChanged()
    {
        UpdateLayout();
        LayoutChanged?.Invoke();
    }

    private void OnDragStarted(Vector2 mouse)
    {
        _grabOffset = mouse - _bar.GlobalPosition;
    }

    private void OnDragged(Vector2 mouse)
    {
        if (_bar.Parent is not { } parent)
            return;

        _layout.SetBarPosition(Clamp(mouse - _grabOffset - parent.GlobalPosition));
    }

    private Vector2 Clamp(Vector2 position)
    {
        if (_bar.Parent is not { } parent)
            return position;

        var max = Vector2.Max(Vector2.Zero, parent.Size - _bar.Size);
        return Vector2.Clamp(position, Vector2.Zero, max);
    }

    private void SetPosition(Vector2 position)
    {
        if (_bar.GetValue<float>(LayoutContainer.MarginLeftProperty).Equals(position.X)
            && _bar.GetValue<float>(LayoutContainer.MarginTopProperty).Equals(position.Y)
            && _bar.GetValue<float>(LayoutContainer.MarginRightProperty).Equals(position.X)
            && _bar.GetValue<float>(LayoutContainer.MarginBottomProperty).Equals(position.Y))
        {
            return;
        }

        // Zero-size margin box, so the bar always takes exactly its desired size and grows right/down from the point.
        LayoutContainer.SetAnchorPreset(_bar, LayoutContainer.LayoutPreset.TopLeft);
        LayoutContainer.SetMarginLeft(_bar, position.X);
        LayoutContainer.SetMarginRight(_bar, position.X);
        LayoutContainer.SetMarginTop(_bar, position.Y);
        LayoutContainer.SetMarginBottom(_bar, position.Y);
    }
}
