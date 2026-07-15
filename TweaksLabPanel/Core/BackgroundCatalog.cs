namespace TweaksLabPanel.Core;

public static class BackgroundCatalog
{
    public sealed record Preset(string Id, string Label, string FileName, DateTime? AddedAt = null);

    private static readonly DateTime NewBatchDate = new(2026, 7, 1);

    public static readonly Preset[] All =
    [
        new("avatar", "Avatar", "bg-avatar.png"),
        new("gon", "Gon", "bg-gon.png"),
        new("pain", "Pain", "bg-pain.png"),
        new("minecraft", "Minecraft", "bg-minecraft.png"),
        new("minecraft2", "Caverne", "bg-minecraft-2.png"),
        new("lake", "Lac", "bg-lake.png"),
        new("gta6", "GTA VI", "bg-gta6.png"),
        new("tanjiro", "Tanjiro", "bg-tanjiro.png"),
        new("luffy", "Luffy", "bg-luffy.png"),
        new("surfsup", "Surf's Up", "bg-surfs-up.png"),
        new("aurora", "Aurora", "aurora.png"),
        new("violet", "Violet", "aurora-purple.png"),
        new("midnight", "Minuit", "aurora-midnight.png"),
        new("samurai", "Samouraï", "aurora-samurai.png"),
        new("flame", "Flamme", "aurora-flame.png"),
        new("sunset", "Plage", "aurora-sunset.png"),
        new("dragon", "Dragon", "aurora-dragon.png"),
        new("balancoire", "Balançoire", "bg-balancoire.png", NewBatchDate),
        new("gta5", "GTA V", "bg-gta5.png", NewBatchDate),
        new("fortnitebus", "Fortnite", "bg-fortnite-bus.png", NewBatchDate),
        new("neonwave", "Neon", "bg-neon-wave.png", NewBatchDate),
        new("klombo", "Valorant", "bg-klombo.png", NewBatchDate),
        new("montagne", "Montagne", "bg-montagne.png", NewBatchDate),
        new("japan", "Japon", "bg-japan.png", NewBatchDate),
    ];

    public static string DefaultId => "japan";

    public static bool IsNew(Preset preset) =>
        preset.AddedAt is not null && DateTime.UtcNow - preset.AddedAt.Value.ToUniversalTime() <= TimeSpan.FromDays(30);

    public static string? FileNameFor(string id) =>
        All.FirstOrDefault(p => p.Id == id)?.FileName;

    public static string[] FileNames => All.Select(p => p.FileName).ToArray();
}
