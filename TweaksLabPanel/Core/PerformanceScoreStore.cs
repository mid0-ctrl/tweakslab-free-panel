using System.IO;
using System.Text.Json;

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
    public PerformanceSnapshot? Snapshot { get; private set; }

    public void Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return;

            var json = File.ReadAllText(_settingsPath);
            var data = JsonSerializer.Deserialize<PerformanceData>(json);
            if (data is null || !data.HasScan)
                return;

            Snapshot = data.ToSnapshot();
            HasScan = true;
        }
        catch
        {
            HasScan = false;
            Snapshot = null;
        }
    }

    public void Save(PerformanceSnapshot snapshot)
    {
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

            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(PerformanceData.From(Snapshot), JsonOptions));
        }
        catch
        {
            // Persistance locale : ignorer si indisponible.
        }
    }

    private sealed class PerformanceData
    {
        // Format 2 : températures réelles (null si capteur absent). Avant, des valeurs par défaut étaient enregistrées.
        // Format 3 : mesures du score de santé 0–90 (réglages Windows, SSD, pilote, écran…).
        private const int CurrentFormat = 3;

        public int Format { get; set; }
        public bool HasScan { get; set; }
        public int InstalledAppsCount { get; set; }
        public int StartupAppsCount { get; set; }
        public int RecentErrorCount { get; set; }
        public double? CpuTempC { get; set; }
        public double? GpuTempC { get; set; }
        public double RamUsedGb { get; set; }
        public double RamTotalGb { get; set; }
        public long TempFolderSizeMb { get; set; }
        public int RunningProcessCount { get; set; }
        public int SystemDriveFreePercent { get; set; }
        public FeatureActivationState Xmp { get; set; } = FeatureActivationState.Unknown;
        public string? XmpDetail { get; set; }
        public double? UptimeDays { get; set; }
        public bool? SystemDriveIsSsd { get; set; }
        public int? RefreshRateHz { get; set; }
        public int? MaxRefreshRateHz { get; set; }
        public bool? PerformancePowerPlan { get; set; }
        public string? PowerPlanName { get; set; }
        public bool? GameModeEnabled { get; set; }
        public bool? BackgroundCaptureOff { get; set; }
        public bool? HardwareGpuScheduling { get; set; }
        public bool? VisualEffectsOptimized { get; set; }
        public bool? MouseAccelerationOff { get; set; }
        public int? GpuDriverAgeDays { get; set; }
        public DateTime ScannedAtUtc { get; set; }

        public static PerformanceData From(PerformanceSnapshot? snapshot) =>
            snapshot is null
                ? new PerformanceData { Format = CurrentFormat }
                : new PerformanceData
                {
                    Format = CurrentFormat,
                    HasScan = true,
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
                    Xmp = snapshot.Xmp,
                    XmpDetail = snapshot.XmpDetail,
                    UptimeDays = snapshot.UptimeDays,
                    SystemDriveIsSsd = snapshot.SystemDriveIsSsd,
                    RefreshRateHz = snapshot.RefreshRateHz,
                    MaxRefreshRateHz = snapshot.MaxRefreshRateHz,
                    PerformancePowerPlan = snapshot.PerformancePowerPlan,
                    PowerPlanName = snapshot.PowerPlanName,
                    GameModeEnabled = snapshot.GameModeEnabled,
                    BackgroundCaptureOff = snapshot.BackgroundCaptureOff,
                    HardwareGpuScheduling = snapshot.HardwareGpuScheduling,
                    VisualEffectsOptimized = snapshot.VisualEffectsOptimized,
                    MouseAccelerationOff = snapshot.MouseAccelerationOff,
                    GpuDriverAgeDays = snapshot.GpuDriverAgeDays,
                    ScannedAtUtc = snapshot.ScannedAtUtc,
                };

        public PerformanceSnapshot ToSnapshot() =>
            new(
                InstalledAppsCount,
                StartupAppsCount,
                RecentErrorCount,
                Format >= 2 ? CpuTempC : null,
                Format >= 2 ? GpuTempC : null,
                RamUsedGb,
                RamTotalGb,
                TempFolderSizeMb,
                RunningProcessCount,
                SystemDriveFreePercent,
                Xmp,
                XmpDetail,
                ScannedAtUtc == default ? DateTime.UtcNow : ScannedAtUtc)
            {
                UptimeDays = UptimeDays,
                SystemDriveIsSsd = SystemDriveIsSsd,
                RefreshRateHz = RefreshRateHz,
                MaxRefreshRateHz = MaxRefreshRateHz,
                PerformancePowerPlan = PerformancePowerPlan,
                PowerPlanName = PowerPlanName,
                GameModeEnabled = GameModeEnabled,
                BackgroundCaptureOff = BackgroundCaptureOff,
                HardwareGpuScheduling = HardwareGpuScheduling,
                VisualEffectsOptimized = VisualEffectsOptimized,
                MouseAccelerationOff = MouseAccelerationOff,
                GpuDriverAgeDays = GpuDriverAgeDays,
            };
    }
}

/// <summary>Score de santé : de 0 à 90, la perfection n'existe pas.</summary>
public static class PerformanceScoreDefaults
{
    public const int MinScore = 0;
    public const int MaxScore = 90;
}
