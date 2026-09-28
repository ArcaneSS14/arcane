using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Shared._Arcane.CCVars;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Arcane.UserInterface.Systems.Actions;

/// <summary>
/// Holds the player's custom actions bar layout: the bar position and positions of detached hotbar slots.
/// Edits made on the HUD are written to the client config shortly after the last change.
/// </summary>
public sealed class ActionsBarLayoutUIController : UIController
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan AutoSaveDelay = TimeSpan.FromSeconds(1);

    private readonly Dictionary<int, Vector2> _slotPositions = new();
    private TimeSpan? _saveAt;

    /// <summary>
    /// Custom top-left position of the bar inside its HUD layout, or null for the screen default.
    /// </summary>
    public Vector2? BarPosition { get; private set; }

    /// <summary>
    /// Slot index to top-left position inside the HUD layout, in virtual pixels.
    /// </summary>
    public IReadOnlyDictionary<int, Vector2> SlotPositions => _slotPositions;

    public bool FreePlacementEnabled { get; private set; }

    /// <summary>
    /// Raised whenever the bar or any slot position changes.
    /// </summary>
    public event Action? LayoutChanged;

    public event Action<bool>? FreePlacementChanged;

    public override void Initialize()
    {
        base.Initialize();

        LoadSaved();
        _cfg.OnValueChanged(ACCVars.ActionsBarFreePlacement, OnFreePlacementChanged, true);
    }

    private void OnFreePlacementChanged(bool enabled)
    {
        FreePlacementEnabled = enabled;
        FreePlacementChanged?.Invoke(enabled);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_saveAt is { } saveAt && _timing.RealTime >= saveAt)
            Save();
    }

    public void SetBarPosition(Vector2 position)
    {
        BarPosition = position;
        QueueSave();
        LayoutChanged?.Invoke();
    }

    public void SetSlotPosition(int slot, Vector2 position)
    {
        _slotPositions[slot] = position;
        QueueSave();
        LayoutChanged?.Invoke();
    }

    public void ClearSlotPosition(int slot)
    {
        if (!_slotPositions.Remove(slot))
            return;

        QueueSave();
        LayoutChanged?.Invoke();
    }

    /// <summary>
    /// Edits are saved shortly after the last change, so a drag does not write the config on every mouse move.
    /// </summary>
    private void QueueSave()
    {
        _saveAt = _timing.RealTime + AutoSaveDelay;
    }

    /// <summary>
    /// Writes the current layout to the client config so it survives restarts.
    /// </summary>
    private void Save()
    {
        _saveAt = null;

        _cfg.SetCVar(ACCVars.ActionsBarPositionX, BarPosition?.X ?? -1f);
        _cfg.SetCVar(ACCVars.ActionsBarPositionY, BarPosition?.Y ?? -1f);

        var entries = _slotPositions.Select(pair => string.Format(CultureInfo.InvariantCulture,
            "{0}:{1:0.##}:{2:0.##}", pair.Key, pair.Value.X, pair.Value.Y));
        _cfg.SetCVar(ACCVars.ActionsBarSlotPositions, string.Join(';', entries));

        _cfg.SaveToFile();
    }

    /// <summary>
    /// Returns the bar and every slot to the default layout and saves that.
    /// </summary>
    public void Reset()
    {
        BarPosition = null;
        _slotPositions.Clear();
        Save();
        LayoutChanged?.Invoke();
    }

    private void LoadSaved()
    {
        var x = _cfg.GetCVar(ACCVars.ActionsBarPositionX);
        var y = _cfg.GetCVar(ACCVars.ActionsBarPositionY);
        BarPosition = x >= 0 && y >= 0 ? new Vector2(x, y) : null;

        _slotPositions.Clear();
        foreach (var entry in _cfg.GetCVar(ACCVars.ActionsBarSlotPositions).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split(':');
            if (parts.Length != 3
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var slot)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var slotX)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var slotY)
                || slot < 0)
            {
                continue;
            }

            _slotPositions[slot] = new Vector2(slotX, slotY);
        }
    }
}
