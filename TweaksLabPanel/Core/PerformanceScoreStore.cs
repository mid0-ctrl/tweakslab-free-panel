using System.IO;
using System.Text.Json;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Core;

public sealed class PerformanceScoreStore
{
    public static PerformanceScoreStore Instance { get; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TweaksLabPanel",
        "performance.json");

    public bool HasScan { get; private set; }
    public int Score { get; private set; }
    public PerformanceSnapshot? Snapshot { get; private set; }

    public void Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return;

            var json = File.ReadAllText(_settingsPath);
            var data = JsonSerializer.Deserialize<PerformanceData>(json);
            if (data is null)
                return;

            HasScan = data.HasScan;
            Score = Math.Clamp(data.Score, PerformanceScoreDefaults.MinScore, PerformanceScoreDefaults.MaxScore);
            Snapshot = data.ToSnapshot();
        }
        catch
        {
            HasScan = false;
            Score = 0;
            Snapshot = null;
        }
    }

    public void Save(int score, PerformanceSnapshot snapshot)
    {
        Score = Math.Clamp(score, PerformanceScoreDefaults.MinScore, PerformanceScoreDefaults.MaxScore);
        Snapshot = snapshot;
        HasScan = true;
        Persist();
    }

    private void Persist()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var data = PerformanceData.From(Score, Snapshot);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch
        {
            // Persistance locale : ignorer si indisponible.
        }
    }

    private sealed class PerformanceData
    {
        public bool HasScan { get; set; }
        public int Score { get; set; }
        public int InstalledAppsCount { get; set; }
        public int StartupAppsCount { get; set; }
        public int RecentErrorCount { get; set; }
        public double CpuTempC { get; set; }
        public double GpuTempC { get; set; }
        public double RamUsedGb { get; set; }
        public double RamTotalGb { get; set; }
        public long TempFolderSizeMb { get; set; }
        public int RunningProcessCount { get; set; }
        public int SystemDriveFreePercent { get; set; }
        public DateTime ScannedAtUtc { get; set; }

        public static PerformanceData From(int score, PerformanceSnapshot? snapshot) =>
            snapshot is null
                ? new PerformanceData { Score = score }
                : new PerformanceData
                {
                    HasScan = true,
                    Score = score,
                    InstalledAppsCount = snapshot.InstalledAppsCount,
                    StartupAppsCount = snapshot.StartupAppsCount,
                    RecentErrorCount = snapshot.RecentErrorCount,
                    CpuTempC = snapshot.CpuTempC,
                    GpuTempC = snapshot.GpuTempC,
                    RamUsedGb = snapshot.RamUsedGb,
                    RamTotalGb = snapshot.RamTotalGb,
                    TempFolderSizeMb = snapshot.TempFolderSizeMb,
                    RunningProcessCount = snapshot.RunningProcessCount,
                    SystemDriveFreePercent = snapshot.SystemDriveFreePercent,
                    ScannedAtUtc = snapshot.ScannedAtUtc,
                };

        public PerformanceSnapshot ToSnapshot() =>
            new(
                InstalledAppsCount,
                StartupAppsCount,
                RecentErrorCount,
                CpuTempC,
                GpuTempC,
                RamUsedGb,
                RamTotalGb,
                TempFolderSizeMb,
                RunningProcessCount,
                SystemDriveFreePercent,
                ScannedAtUtc == default ? DateTime.UtcNow : ScannedAtUtc);
    }
}

public static class PerformanceScoreDefaults
{
    public const int MinScore = 55;
    public const int MaxScore = 80;
}
