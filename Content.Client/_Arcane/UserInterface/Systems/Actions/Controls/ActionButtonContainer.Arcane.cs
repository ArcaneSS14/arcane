using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client._Arcane.UserInterface.Systems.Actions.Controls;
using Content.Shared._Arcane.CCVars;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.Actions.Controls;

/// <summary>
/// Arcane: lets individual hotbar slots be detached from the grid and placed anywhere inside <see cref="PositionSpace"/>.
/// Detached buttons stay children of this container at the same index, so slot numbers, hotkeys and
/// the action list are unaffected; only their layout changes.
/// </summary>
public partial class ActionButtonContainer
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private const float DefaultSeparation = 4f;

    /// <summary>
    /// Slot index to top-left position inside <see cref="PositionSpace"/>, in virtual pixels.
    /// </summary>
    private readonly Dictionary<int, Vector2> _slotPositions = new();

    private Vector2 _spaceOffset;
    private Vector2 _spaceSize;

    /// <summary>
    /// Control whose area detached slots are positioned and clamped in, usually the HUD layout the bar lives in.
    /// </summary>
    public Control? PositionSpace { get; set; }

    public bool IsSlotDetached(int slot)
    {
        return _slotPositions.ContainsKey(slot);
    }

    /// <summary>
    /// Handles dropping a dragged hotbar button somewhere that is not another action button.
    /// Dropping on the bar's grip returns the slot to the grid, dropping on free HUD space moves the slot there.
    /// </summary>
    /// <returns>False if the drop should fall back to the default hotbar behavior.</returns>
    public bool TryHandleSlotDrop(ActionButton button, Control? dropTarget, Vector2 mousePosition)
    {
        if (!TryGetButtonIndex(button, out var slot))
            return false;

        if (dropTarget is ActionsBarDragHandle)
        {
            if (_slotPositions.Remove(slot))
                SaveSlotPositions();

            return true;
        }

        if (PositionSpace is not { } space || dropTarget != null && IsInsideWindow(dropTarget))
            return false;

        var position = mousePosition - space.GlobalPosition - button.Size / 2;
        _slotPositions[slot] = Vector2.Clamp(position, Vector2.Zero, Vector2.Max(Vector2.Zero, space.Size - button.Size));
        SaveSlotPositions();
        return true;
    }

    public void ResetSlotPositions()
    {
        if (_slotPositions.Count == 0)
            return;

        _slotPositions.Clear();
        SaveSlotPositions();
    }

    private void LoadSlotPositions()
    {
        _slotPositions.Clear();

        foreach (var entry in _cfg.GetCVar(ACCVars.ActionsBarSlotPositions).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split(':');
            if (parts.Length != 3
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var slot)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                || slot < 0)
            {
                continue;
            }

            _slotPositions[slot] = new Vector2(x, y);
        }
    }

    private void SaveSlotPositions()
    {
        var entries = _slotPositions.Select(pair => string.Create(CultureInfo.InvariantCulture,
            $"{pair.Key}:{pair.Value.X:0.##}:{pair.Value.Y:0.##}"));

        _cfg.SetCVar(ACCVars.ActionsBarSlotPositions, string.Join(';', entries));
        _cfg.SaveToFile();

        InvalidateMeasure();
        InvalidateArrange();
    }

    private bool HasDetachedSlots()
    {
        foreach (var slot in _slotPositions.Keys)
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

            if (_slotPositions.TryGetValue(i, out var position))
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
            if (GetChild(i).Visible && !_slotPositions.ContainsKey(i))
                count++;
        }

        return count;
    }

    /// <summary>
    /// How many cells fit along the limited dimension for the given amount of grid-placed slots.
    /// </summary>
    private int GetFlowLimit(int flowCount)
    {
        var limit = LimitedDimension == Dimension.Column ? Columns : Rows;
        if (LimitType == LimitType.Size)
            limit = Math.Min(limit, flowCount);

        return Math.Max(1, limit);
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

        var limit = GetFlowLimit(flowCount);
        var other = (flowCount + limit - 1) / limit;

        return LimitedDimension == Dimension.Column
            ? (limit, other, cell, separation)
            : (other, limit, cell, separation);
    }
}
