using System.Numerics;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Arcane.UserInterface.Systems.Actions;

/// <summary>
/// Places the actions bar at its default position or at the one chosen by the player.
/// </summary>
public sealed class ActionsBarPlacement
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private readonly ActionsBar _bar;
    private readonly Func<Vector2> _defaultPosition;
    private readonly ActionsBarLayoutUIController _layout;

    private Vector2 _grabOffset;
    private bool _updateQueued;

    public bool IsCustom => _layout.BarPosition != null;

    public event Action? LayoutChanged;

    public ActionsBarPlacement(ActionsBar bar, Func<Vector2> defaultPosition)
    {
        IoCManager.InjectDependencies(this);

        _bar = bar;
        _defaultPosition = defaultPosition;
        _layout = _ui.GetUIController<ActionsBarLayoutUIController>();

        _bar.ActionsContainer.PositionSpace = _bar.Parent;
        _bar.ActionsContainer.ArcaneLayoutChanged += OnLayoutChanged;

        _bar.ActionsContainer.ReturnHintChanged += _bar.DragHandle.SetHighlighted;
        _bar.DragHandle.DragStarted += OnDragStarted;
        _bar.DragHandle.Dragged += OnDragged;
        _bar.DragHandle.ResetRequested += _layout.Reset;

        _bar.OnResized += QueueUpdateLayout;
        if (_bar.Parent != null)
            _bar.Parent.OnResized += QueueUpdateLayout;
    }

    public void UpdateLayout()
    {
        SetPosition(_layout.BarPosition is { } custom ? Clamp(custom) : _defaultPosition());
    }

    // OnResized fires during the parent's arrange, and changing margins at that point does not trigger
    // another arrange (InvalidateArrange is ignored), so the bar would stay at its old position
    public void QueueUpdateLayout()
    {
        if (_updateQueued)
            return;

        _updateQueued = true;
        Timer.Spawn(0, () =>
        {
            _updateQueued = false;
            UpdateLayout();
            LayoutChanged?.Invoke();
        });
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
        _bar.ActionsContainer.SetBarPlacement(_bar, position);

        if (_bar.GetValue<float>(LayoutContainer.MarginLeftProperty).Equals(position.X)
            && _bar.GetValue<float>(LayoutContainer.MarginTopProperty).Equals(position.Y)
            && _bar.GetValue<float>(LayoutContainer.MarginRightProperty).Equals(position.X)
            && _bar.GetValue<float>(LayoutContainer.MarginBottomProperty).Equals(position.Y))
        {
            return;
        }

        // Zero-size margin box: the bar takes its desired size and grows right and down from the point
        LayoutContainer.SetAnchorPreset(_bar, LayoutContainer.LayoutPreset.TopLeft);
        LayoutContainer.SetMarginLeft(_bar, position.X);
        LayoutContainer.SetMarginRight(_bar, position.X);
        LayoutContainer.SetMarginTop(_bar, position.Y);
        LayoutContainer.SetMarginBottom(_bar, position.Y);
    }
}
