using System.Numerics;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Shared._Arcane.CCVars;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client._Arcane.UserInterface.Systems.Actions;

/// <summary>
/// Places the actions bar inside its parent <see cref="LayoutContainer"/>: either at the screen's default spot
/// or at a player-chosen position that is dragged with the bar's grip and persisted in client CVars.
/// </summary>
public sealed class ActionsBarPlacement
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private readonly ActionsBar _bar;
    private readonly Func<Vector2> _defaultPosition;

    private Vector2? _customPosition;
    private Vector2 _grabOffset;

    public bool IsCustom => _customPosition != null;

    /// <summary>
    /// Raised when the bar switches between the default and a custom position.
    /// </summary>
    public event Action? CustomChanged;

    public ActionsBarPlacement(ActionsBar bar, Func<Vector2> defaultPosition)
    {
        IoCManager.InjectDependencies(this);

        _bar = bar;
        _defaultPosition = defaultPosition;

        var x = _cfg.GetCVar(ACCVars.ActionsBarPositionX);
        var y = _cfg.GetCVar(ACCVars.ActionsBarPositionY);
        if (x >= 0 && y >= 0)
            _customPosition = new Vector2(x, y);

        _bar.DragHandle.DragStarted += OnDragStarted;
        _bar.DragHandle.Dragged += OnDragged;
        _bar.DragHandle.DragFinished += OnDragFinished;
        _bar.DragHandle.ResetRequested += OnResetRequested;

        _bar.OnResized += UpdateLayout;
        if (_bar.Parent != null)
            _bar.Parent.OnResized += UpdateLayout;
    }

    /// <summary>
    /// Re-applies the current position. Call it when anything the default position depends on changes size.
    /// </summary>
    public void UpdateLayout()
    {
        SetPosition(_customPosition is { } custom ? Clamp(custom) : _defaultPosition());
    }

    private void OnDragStarted(Vector2 mouse)
    {
        _grabOffset = mouse - _bar.GlobalPosition;
    }

    private void OnDragged(Vector2 mouse)
    {
        if (_bar.Parent is not { } parent)
            return;

        var wasCustom = IsCustom;
        _customPosition = Clamp(mouse - _grabOffset - parent.GlobalPosition);
        SetPosition(_customPosition.Value);

        if (!wasCustom)
            CustomChanged?.Invoke();
    }

    private void OnDragFinished()
    {
        if (_customPosition is not { } custom)
            return;

        _cfg.SetCVar(ACCVars.ActionsBarPositionX, custom.X);
        _cfg.SetCVar(ACCVars.ActionsBarPositionY, custom.Y);
        _cfg.SaveToFile();
    }

    private void OnResetRequested()
    {
        var wasCustom = IsCustom;
        _customPosition = null;

        _cfg.SetCVar(ACCVars.ActionsBarPositionX, -1f);
        _cfg.SetCVar(ACCVars.ActionsBarPositionY, -1f);
        _cfg.SaveToFile();

        UpdateLayout();

        if (wasCustom)
            CustomChanged?.Invoke();
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
