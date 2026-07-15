using System.IO;
using System.Text.Json;

namespace TweaksLabPanel.Core;

public static class TweaksLabPowerPlanState
{
    private sealed class Store
    {
        public string? PlanGuid { get; set; }
        public string? FallbackGuid { get; set; }
        public string? ExportPath { get; set; }
        public bool Installed { get; set; }
    }

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TweaksLabPanel",
        "power-plan.json");

    private static Store? _cache;

    public static string? PlanGuid
    {
        get => EnsureLoaded().PlanGuid;
        set { var store = EnsureLoaded(); store.PlanGuid = value; Save(store); }
    }

    public static string? FallbackGuid
    {
        get => EnsureLoaded().FallbackGuid;
        set { var store = EnsureLoaded(); store.FallbackGuid = value; Save(store); }
    }

    public static string? ExportPath
    {
        get => EnsureLoaded().ExportPath;
        set { var store = EnsureLoaded(); store.ExportPath = value; Save(store); }
    }

    public static bool Installed
    {
        get => EnsureLoaded().Installed;
        set { var store = EnsureLoaded(); store.Installed = value; Save(store); }
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
