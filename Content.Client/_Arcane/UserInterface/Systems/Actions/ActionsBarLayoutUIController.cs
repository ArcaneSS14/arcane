using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Shared._Arcane.CCVars;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Arcane.UserInterface.Systems.Actions;

/// <summary>
/// Stores the actions bar and detached slot layout and saves it to the config shortly after the last change.
/// </summary>
public sealed class ActionsBarLayoutUIController : UIController
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan AutoSaveDelay = TimeSpan.FromSeconds(1);

    private readonly Dictionary<int, Vector2> _slotPositions = new();
    private TimeSpan? _saveAt;

    public Vector2? BarPosition { get; private set; }

    public IReadOnlyDictionary<int, Vector2> SlotPositions => _slotPositions;

    public bool FreePlacementEnabled { get; private set; }

    public event Action? LayoutChanged;

    public override void Initialize()
    {
        base.Initialize();

        LoadSaved();
        _cfg.OnValueChanged(ACCVars.ActionsBarFreePlacement, enabled => FreePlacementEnabled = enabled, true);
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

    private void QueueSave()
    {
        _saveAt = _timing.RealTime + AutoSaveDelay;
    }

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
