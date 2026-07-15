using System.IO;
using System.Text.Json;

namespace TweaksLabPanel.Core;

public static class WallpaperSelectionState
{
    private sealed class Store
    {
        public int? LastAppliedIndex { get; set; }
    }

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TweaksLabPanel",
        "wallpaper-selection.json");

    private static Store? _cache;

    public static int? LastAppliedIndex
    {
        get => EnsureLoaded().LastAppliedIndex;
        set
        {
            var store = EnsureLoaded();
            store.LastAppliedIndex = value;
            Save(store);
        }
    }

    private static Store EnsureLoaded()
    {
        if (_cache is not null)
            return _cache;

        try
        {
            if (File.Exists(StorePath))
            {
                _cache = JsonSerializer.Deserialize<Store>(File.ReadAllText(StorePath)) ?? new Store();
                return _cache;
            }
        }
        catch
        {
            // Ignorer.
        }

        _cache = new Store();
        return _cache;
    }

    private static void Save(Store store)
    {
        _cache = store;
        try
        {
            var directory = Path.GetDirectoryName(StorePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(StorePath, JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Ignorer.
        }
    }
}
