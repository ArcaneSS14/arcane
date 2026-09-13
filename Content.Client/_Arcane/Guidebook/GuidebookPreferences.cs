using Robust.Shared.ContentPack;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.Client._Arcane.Guidebook;

public sealed class GuidebookPreferences
{
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly ILogManager _log = default!;
    private readonly ISawmill _sawmill;

    private static readonly ResPath Path = new("/arcane-guidebook.yml");
    private readonly IResourceManager _resources;

    public GuidebookWindowState Window { get; private set; } = new();
    public HashSet<string> Favorites { get; private set; } = new();
    public event Action? FavoritesChanged;

    public GuidebookPreferences(IResourceManager resources)
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _log.GetSawmill("Guidebook");
        _resources = resources;
        try
        {
            if (!resources.UserData.Exists(Path))
                return;

            using var reader = resources.UserData.OpenText(Path);
            var stream = new YamlStream();
            stream.Load(reader);
            if (stream.Documents.Count == 0)
                return;

            var saved = _serialization.Read<GuidebookSavedPreferences>(
                stream.Documents[0].RootNode.ToDataNode(), notNullableOverride: true);
            Window = saved.Window ?? new();
            Favorites = saved.Favorites ?? new();
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Unable to load guidebook preferences: {e.Message}");
        }
    }

    public void ToggleFavorite(string entry)
    {
        if (!Favorites.Remove(entry))
            Favorites.Add(entry);
        Save();
        FavoritesChanged?.Invoke();
    }

    public void SaveWindow(GuidebookWindowState state)
    {
        Window = state;
        Save();
    }

    private void Save()
    {
        try
        {
            var data = _serialization.WriteValue(
                new GuidebookSavedPreferences { Window = Window, Favorites = Favorites }, notNullableOverride: true);
            var stream = new YamlStream(new YamlDocument(data.ToYamlNode()));
            using var writer = _resources.UserData.OpenWriteText(Path);
            stream.Save(writer);
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Unable to save guidebook preferences: {e.Message}");
        }
    }
}

[DataDefinition]
public sealed partial class GuidebookSavedPreferences
{
    [DataField] public GuidebookWindowState? Window { get; set; }
    [DataField] public HashSet<string>? Favorites { get; set; }
}

[DataDefinition]
public sealed partial class GuidebookWindowState
{
    [DataField] public float Width { get; set; } = 1000;
    [DataField] public float Height { get; set; } = 700;
    [DataField] public float SidebarWidth { get; set; } = 260;
    [DataField] public bool SidebarHidden { get; set; }
    [DataField] public string? Category { get; set; }
    [DataField] public string? Entry { get; set; }
    [DataField] public Dictionary<string, bool> Expanded { get; set; } = new();
    [DataField] public Dictionary<string, float> Scroll { get; set; } = new();
}
