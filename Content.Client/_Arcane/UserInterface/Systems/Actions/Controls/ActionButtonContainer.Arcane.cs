using System.Numerics;
using Content.Client._Arcane.UserInterface.Systems.Actions;
using Content.Client._Arcane.UserInterface.Systems.Actions.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.Actions.Controls;

/// <summary>
/// Arcane: lets individual hotbar slots be detached from the grid and placed anywhere inside <see cref="PositionSpace"/>.
/// Detached buttons stay children of this container at the same index, so slot numbers, hotkeys and
/// the action list are unaffected; only their layout changes.
/// </summary>
public partial class ActionButtonContainer
{
    private const float DefaultSeparation = 4f;

    private ActionsBarLayoutUIController? _layout;

    private Vector2 _spaceOffset;
    private Vector2 _spaceSize;

    private ActionsBarLayoutUIController Layout =>
        _layout ??= UserInterfaceManager.GetUIController<ActionsBarLayoutUIController>();

    /// <summary>
    /// Control whose area detached slots are positioned and clamped in, usually the HUD layout the bar lives in.
    /// </summary>
    public Control? PositionSpace { get; set; }

    /// <summary>
    /// Raised when the saved or edited actions bar layout changes.
    /// </summary>
    public event Action? ArcaneLayoutChanged;

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
        InvalidateMeasure();
        InvalidateArrange();
        ArcaneLayoutChanged?.Invoke();
    }

    /// <summary>
    /// Handles dropping a dragged hotbar button somewhere that is not another action button while free placement is on.
    /// Dropping on the bar's grip returns the slot to the grid, dropping on free HUD space moves the slot there.
    /// </summary>
    /// <returns>False if the drop should fall back to the default hotbar behavior.</returns>
    public bool TryHandleSlotDrop(ActionButton button, Control? dropTarget, Vector2 mousePosition)
    {
        if (!Layout.FreePlacementEnabled || !TryGetButtonIndex(button, out var slot))
            return false;

        if (dropTarget is ActionsBarDragHandle)
        {
            Layout.ClearSlotPosition(slot);
            return true;
        }

        if (PositionSpace is not { } space || dropTarget != null && IsInsideWindow(dropTarget))
            return false;

        var position = mousePosition - space.GlobalPosition - button.Size / 2;
        Layout.SetSlotPosition(slot,
            Vector2.Clamp(position, Vector2.Zero, Vector2.Max(Vector2.Zero, space.Size - button.Size)));
        return true;
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

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (PositionSpace is not { } space || !HasDetachedSlots())
            return;

        // Our own position is only final after ArrangeOverride, so track the offset here and re-arrange when it moves.
        var offset = space.GlobalPosition - GlobalPosition;
        if (offset == _spaceOffset && space.Size == _spaceSize)
            return;

        _spaceOffset = offset;
        _spaceSize = space.Size;
        InvalidateArrange();
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

            // Same fill order as GridContainer: along the limited dimension first.
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

        // How many cells fit along the limited dimension for the grid-placed slots only.
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
