namespace TweaksLabPanel.Core;

public static class WallpaperCatalog
{
    public const string CollectionName = "Collection TweaksLab";

    public sealed record Preset(int Index, string Id, string Label, string FileName);

    public static readonly Preset[] All =
    [
        new(1, "violet", "Violet", "wallpaper_1.png"),
        new(2, "aurore", "Aurore", "wallpaper_2.png"),
        new(3, "pulse", "Pulse", "wallpaper_3.png"),
        new(4, "minuit", "Minuit", "wallpaper_4.png"),
        new(5, "balancoire", "Balançoire", "wallpaper_5.png"),
        new(6, "gta5", "GTA V", "wallpaper_6.png"),
        new(7, "fortnite", "Fortnite", "wallpaper_7.png"),
        new(8, "neon", "Neon", "wallpaper_8.png"),
        new(9, "klombo", "Valorant", "wallpaper_9.png"),
        new(10, "montagne", "Montagne", "wallpaper_10.png"),
        new(11, "japan", "Japon", "wallpaper_11.png"),
    ];

    public static Preset? FindByIndex(int index) =>
        All.FirstOrDefault(p => p.Index == index);

    public static Preset[] Featured =>
    [
        All[4],
        All[5],
        All[6],
        All[7],
        All[8],
    ];
}
