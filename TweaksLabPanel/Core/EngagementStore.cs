using System.IO;
using System.Text.Json;

namespace TweaksLabPanel.Core;

public sealed class EngagementStore
{
    public static EngagementStore Instance { get; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TweaksLabPanel",
        "engagement.json");

    public int CurrentStreak { get; private set; }
    public int LongestStreak { get; private set; }

    public void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;

            var data = JsonSerializer.Deserialize<EngagementData>(File.ReadAllText(_path));
            if (data is null)
                return;

            CurrentStreak = Math.Max(0, data.CurrentStreak);
            LongestStreak = Math.Max(0, data.LongestStreak);
            _lastOpenDate = data.LastOpenDate;
        }
        catch
        {
            CurrentStreak = 0;
            LongestStreak = 0;
            _lastOpenDate = null;
        }
    }

    private DateOnly? _lastOpenDate;

    public void RecordAppOpen()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        if (_lastOpenDate is null)
        {
            CurrentStreak = 1;
        }
        else if (_lastOpenDate.Value == today)
        {
            // Déjà compté aujourd'hui.
        }
        else if (_lastOpenDate.Value.AddDays(1) == today)
        {
            CurrentStreak++;
        }
        else
        {
            CurrentStreak = 1;
        }

        LongestStreak = Math.Max(LongestStreak, CurrentStreak);
        _lastOpenDate = today;
        Persist();
    }

    private void Persist()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var data = new EngagementData
            {
                LastOpenDate = _lastOpenDate,
                CurrentStreak = CurrentStreak,
                LongestStreak = LongestStreak,
            };
            File.WriteAllText(_path, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch
        {
            // Ignorer.
        }
    }

    private sealed class EngagementData
    {
        public DateOnly? LastOpenDate { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
    }
}
