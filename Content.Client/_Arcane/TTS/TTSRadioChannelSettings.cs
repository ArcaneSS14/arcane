using System.Globalization;
using System.Linq;

namespace Content.Client._Arcane.TTS;

/// <summary>
/// Shared helpers for parsing and formatting per-channel TTS radio settings stored in CVars.
/// </summary>
internal static class TTSRadioChannelSettings
{
    private const char Separator = ';';
    private const char VolumeSeparator = '=';

    public static Dictionary<int, float> ParseVolumes(string value)
    {
        var volumes = new Dictionary<int, float>();
        if (string.IsNullOrWhiteSpace(value))
            return volumes;

        foreach (var part in value.Split(Separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var sep = part.IndexOf(VolumeSeparator);
            if (sep <= 0)
                continue;

            if (!int.TryParse(part.AsSpan(0, sep), NumberStyles.Integer, CultureInfo.InvariantCulture, out var frequency))
                continue;

            if (!float.TryParse(part.AsSpan(sep + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var volume))
                continue;

            if (!float.IsFinite(volume))
                continue;

            volumes[frequency] = Math.Clamp(volume, 0f, 1f);
        }

        return volumes;
    }

    public static HashSet<int> ParseMuted(string value)
    {
        var muted = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(value))
            return muted;

        foreach (var part in value.Split(Separator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frequency))
                muted.Add(frequency);
        }

        return muted;
    }

    public static string FormatVolumes(Dictionary<int, float> volumes)
    {
        return string.Join(Separator, volumes
            .OrderBy(pair => pair.Key)
            .Select(pair => $"{pair.Key}{VolumeSeparator}{pair.Value.ToString("0.##", CultureInfo.InvariantCulture)}"));
    }

    public static string UpsertVolume(string value, int frequency, float volume)
    {
        if (!float.IsFinite(volume))
            volume = 1f;

        var volumes = ParseVolumes(value);
        volumes[frequency] = Math.Clamp(volume, 0f, 1f);
        return FormatVolumes(volumes);
    }

    public static string FormatMuted(HashSet<int> muted)
    {
        return string.Join(Separator, muted.OrderBy(frequency => frequency));
    }

    public static string FormatMutedToggle(string value, int frequency, bool muted)
    {
        var frequencies = ParseMuted(value);
        if (muted)
            frequencies.Add(frequency);
        else
            frequencies.Remove(frequency);

        return FormatMuted(frequencies);
    }

}
