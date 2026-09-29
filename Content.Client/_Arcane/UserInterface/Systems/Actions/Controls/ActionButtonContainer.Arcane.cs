using System.Numerics;
using Content.Client._Arcane.UserInterface.Systems.Actions;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.Actions.Controls;

/// <summary>
/// Позволяет вынести отдельные слоты хотбара из сетки. Кнопка остаётся дочерней с тем же индексом,
/// поэтому хоткеи и порядок действий не меняются.
/// </summary>
public partial class ActionButtonContainer
{
    private const float DefaultSeparation = 4f;

    private ActionsBarLayoutUIController? _layout;

    private const float ReturnZonePadding = 32f;

    private const int MaxCorrections = 8;

    private Vector2 _offsetCorrection;
    private int _correctionsInARow;
    private bool _returnHint;
    private Control? _bar;
    private Vector2? _barPosition;
    private Vector2 _spaceOffset;
    private Vector2 _spaceSize;

    private ActionsBarLayoutUIController Layout =>
        _layout ??= UserInterfaceManager.GetUIController<ActionsBarLayoutUIController>();

    public Control? PositionSpace { get; set; }

    public event Action? ArcaneLayoutChanged;

    public event Action<bool>? ReturnHintChanged;

    protected override void EnteredTree()
    {
        base.EnteredTree();
        Layout.LayoutChanged += OnLayoutChanged;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        Layout.LayoutChanged -= OnLayoutChanged;
    }

    private void OnLayoutChanged()
    {
        _correctionsInARow = 0;
        InvalidateMeasure();
        InvalidateArrange();
        ArcaneLayoutChanged?.Invoke();
    }

    public bool TryHandleSlotDrop(ActionButton button, Control? dropTarget, Vector2 mousePosition)
    {
        if (!Layout.FreePlacementEnabled || !TryGetButtonIndex(button, out var slot))
            return false;

        var detached = Layout.SlotPositions.ContainsKey(slot);

        // Бросок на другую кнопку меняет действия местами, кроме возврата вынесенного слота на панель
        if (dropTarget is ActionButton && dropTarget != button && (!detached || IsDetachedButton(dropTarget)))
            return false;

        if (PositionSpace is not { } space || dropTarget != null && IsInsideWindow(dropTarget))
            return false;

        // Вынесенный слот возвращается в панель, а слот из панели остаётся на месте
        if (IsInReturnZone(mousePosition))
        {
            if (detached)
                Layout.ClearSlotPosition(slot);

            return true;
        }

        var position = mousePosition - space.GlobalPosition - button.Size / 2;
        Layout.SetSlotPosition(slot,
            Vector2.Clamp(position, Vector2.Zero, Vector2.Max(Vector2.Zero, space.Size - button.Size)));
        return true;
    }

    private bool IsDetachedButton(Control? target)
    {
        return target is ActionButton button
            && button.Parent == this
            && TryGetButtonIndex(button, out var index)
            && Layout.SlotPositions.ContainsKey(index);
    }

    private bool IsInReturnZone(Vector2 mousePosition)
    {
        if (_bar == null)
            return false;

        var padding = new Vector2(ReturnZonePadding);
        var zone = new UIBox2(_bar.GlobalPosition - padding, _bar.GlobalPosition + _bar.Size + padding);
        return zone.Contains(mousePosition);
    }

    public void UpdateReturnHint(ActionButton? dragged, Vector2 mousePosition)
    {
        var show = Layout.FreePlacementEnabled
            && dragged != null
            && TryGetButtonIndex(dragged, out var slot)
            && Layout.SlotPositions.ContainsKey(slot)
            && IsInReturnZone(mousePosition);

        if (show == _returnHint)
            return;

        _returnHint = show;
        ReturnHintChanged?.Invoke(show);
    }

    private bool HasDetachedSlots()
    {
        foreach (var slot in Layout.SlotPositions.Keys)
        {
            if (slot < ChildCount)
                return true;
        }

        return false;
    }

    private static bool IsInsideWindow(Control control)
    {
        for (Control? current = control; current != null; current = current.Parent)
        {
            if (current is BaseWindow)
                return true;
        }

        return false;
    }

    // Глобальные позиции финальны только после компоновки, поэтому без явной позиции слоты отставали бы на кадр
    public void SetBarPlacement(Control bar, Vector2 position)
    {
        if (_bar == bar && _barPosition == position)
            return;

        _bar = bar;
        _barPosition = position;
        InvalidateArrange();
    }

    private Vector2 GetSpaceOffset(Control space)
    {
        if (_bar != null && _barPosition is { } barPosition)
            return -(barPosition + (GlobalPosition - _bar.GlobalPosition)) + _offsetCorrection;

        return space.GlobalPosition - GlobalPosition + _offsetCorrection;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (PositionSpace is not { } space || !HasDetachedSlots())
            return;

        var offset = GetSpaceOffset(space);
        if (offset != _spaceOffset || space.Size != _spaceSize)
        {
            _correctionsInARow = 0;
            InvalidateArrange();
            return;
        }

        VerifyDetachedPlacement(space);
    }

    // Смещение считается до конца компоновки (например, при старте HUD) и может быть неточным,
    // поэтому сверяем реальное положение слота с ожидаемым и подправляем смещение
    private void VerifyDetachedPlacement(Control space)
    {
        if (!IsArrangeValid)
            return;

        foreach (var (slot, position) in Layout.SlotPositions)
        {
            if (slot >= ChildCount)
                continue;

            var child = GetChild(slot);
            if (!child.IsArrangeValid)
                continue;

            var size = child.DesiredSize;
            var max = Vector2.Max(Vector2.Zero, space.Size - size);
            var expected = space.GlobalPosition + Vector2.Clamp(position, Vector2.Zero, max);
            var error = expected - child.GlobalPosition;

            if (error.LengthSquared() < 0.25f)
            {
                _correctionsInARow = 0;
                return;
            }

            // Ограничение на случай, когда компоновка никогда не сойдётся
            if (_correctionsInARow++ >= MaxCorrections)
                return;

            _offsetCorrection += error;
            InvalidateArrange();
            return;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (!HasDetachedSlots())
            return base.MeasureOverride(availableSize);

        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }

        var (columns, rows, cell, separation) = GetFlowGrid();
        if (columns == 0 || rows == 0)
            return Vector2.Zero;

        return new Vector2(
            columns * cell.X + (columns - 1) * separation.X,
            rows * cell.Y + (rows - 1) * separation.Y);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (!HasDetachedSlots())
            return base.ArrangeOverride(finalSize);

        if (PositionSpace is { } space)
        {
            _spaceOffset = GetSpaceOffset(space);
            _spaceSize = space.Size;
        }

        var (columns, rows, cell, separation) = GetFlowGrid();
        var flowIndex = 0;

        for (var i = 0; i < ChildCount; i++)
        {
            var child = GetChild(i);

            if (Layout.SlotPositions.TryGetValue(i, out var position))
            {
                var size = child.DesiredSize;
                var max = Vector2.Max(Vector2.Zero, _spaceSize - size);
                child.Arrange(UIBox2.FromDimensions(Vector2.Clamp(position, Vector2.Zero, max) + _spaceOffset, size));
                continue;
            }

            if (!child.Visible)
                continue;

            // Порядок заполнения как в GridContainer
            var (column, row) = LimitedDimension == Dimension.Column
                ? (flowIndex % columns, flowIndex / columns)
                : (flowIndex / rows, flowIndex % rows);

            child.Arrange(UIBox2.FromDimensions(new Vector2(column, row) * (cell + separation), cell));
            flowIndex++;
        }

        return finalSize;
    }

    private int CountFlowSlots()
    {
        var count = 0;
        for (var i = 0; i < ChildCount; i++)
        {
            if (GetChild(i).Visible && !Layout.SlotPositions.ContainsKey(i))
                count++;
        }

        return count;
    }

    private (int Columns, int Rows, Vector2 Cell, Vector2 Separation) GetFlowGrid()
    {
        var cell = Vector2.Zero;
        foreach (var child in Children)
        {
            cell = Vector2.Max(cell, child.DesiredSize);
        }

        var separation = new Vector2(
            HSeparationOverride ?? DefaultSeparation,
            VSeparationOverride ?? DefaultSeparation);

        var flowCount = CountFlowSlots();
        if (flowCount == 0)
            return (0, 0, cell, separation);

        var limit = LimitedDimension == Dimension.Column ? Columns : Rows;
        if (LimitType == LimitType.Size)
            limit = Math.Min(limit, flowCount);
        limit = Math.Max(1, limit);

        var other = (flowCount + limit - 1) / limit;

        return LimitedDimension == Dimension.Column
            ? (limit, other, cell, separation)
            : (other, limit, cell, separation);
    }
}
