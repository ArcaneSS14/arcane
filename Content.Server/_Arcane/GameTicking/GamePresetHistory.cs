using Content.Server.GameTicking.Presets;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.GameTicking;

public sealed class GamePresetHistory
{
    private readonly Queue<ProtoId<GamePresetPrototype>> _presets = new();
    private ProtoId<GamePresetPrototype>? _previousPreset;
    private int _capacity;

    public int Capacity
    {
        get => _capacity;
        set
        {
            _capacity = Math.Max(0, value);
            Trim();
        }
    }

    public void Record(ProtoId<GamePresetPrototype> preset)
    {
        _previousPreset = preset;
        _presets.Enqueue(preset);
        Trim();
    }

    public float GetWeight(ProtoId<GamePresetPrototype> preset, float repeatMultiplier, float previousRoundMultiplier)
    {
        var multiplier = ClampMultiplier(repeatMultiplier);
        var weight = 1f;
        foreach (var previous in _presets)
        {
            if (previous == preset)
                weight *= multiplier;
        }

        if (_previousPreset == preset)
            weight *= ClampMultiplier(previousRoundMultiplier);

        return Math.Max(float.Epsilon, weight);
    }

    private static float ClampMultiplier(float multiplier)
    {
        return float.IsFinite(multiplier) ? Math.Clamp(multiplier, 0.01f, 1f) : 0.5f;
    }

    private void Trim()
    {
        while (_presets.Count > _capacity)
            _presets.Dequeue();
    }
}
