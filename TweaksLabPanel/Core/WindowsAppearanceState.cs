using System.IO;
using System.Text.Json;

namespace TweaksLabPanel.Core;

public sealed class AppearanceProfileSnapshot
{
    public int VisualFxSetting { get; set; } = 1;
    public byte[] UserPreferencesMask { get; set; } = [0x9E, 0x3E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00];
    public int ListviewAlphaSelect { get; set; } = 1;
    public int ListviewShadow { get; set; } = 1;
    public int TaskbarAnimations { get; set; } = 1;
    public int DisablePreviewDesktop { get; set; }
    public int ListviewSmoothScrolling { get; set; } = 1;
    public int DisableThumbnails { get; set; }
    public string DragFullWindows { get; set; } = "1";
    public string FontSmoothing { get; set; } = "2";
    public int FontSmoothingType { get; set; } = 2;
    public string MinAnimate { get; set; } = "1";
}

public static class WindowsAppearanceState
{
    private sealed class Store
    {
        public bool Active { get; set; }
        public AppearanceProfileSnapshot? RestoredProfile { get; set; }
    }

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TweaksLabPanel",
        "windows-appearance.json");

    private static Store? _cache;

    public static bool Active
    {
        get => EnsureLoaded().Active;
        set { var store = EnsureLoaded(); store.Active = value; Save(store); }
    }

    public static AppearanceProfileSnapshot? RestoredProfile
    {
        get => EnsureLoaded().RestoredProfile;
        set { var store = EnsureLoaded(); store.RestoredProfile = value; Save(store); }
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
