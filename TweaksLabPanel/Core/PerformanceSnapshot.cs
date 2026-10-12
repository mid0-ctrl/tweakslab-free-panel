namespace TweaksLabPanel.Core;

/// <summary>
/// Mesures réelles du PC pour le score de santé.
/// null = mesure impossible sur ce PC (jamais de valeur inventée ; le critère est alors exclu du score).
/// </summary>
public sealed record PerformanceSnapshot(
    int InstalledAppsCount,
    int StartupAppsCount,
    int RecentErrorCount,
    double? CpuTempC,
    double? GpuTempC,
    double RamUsedGb,
    double RamTotalGb,
    long TempFolderSizeMb,
    int RunningProcessCount,
    int SystemDriveFreePercent,
    FeatureActivationState Xmp,
    string? XmpDetail,
    DateTime ScannedAtUtc)
{
    /// <summary>Jours depuis le dernier vrai redémarrage de Windows.</summary>
    public double? UptimeDays { get; init; }

    public bool? SystemDriveIsSsd { get; init; }

    public int? RefreshRateHz { get; init; }

    public int? MaxRefreshRateHz { get; init; }

    public bool? PerformancePowerPlan { get; init; }

    public string? PowerPlanName { get; init; }

    public bool? GameModeEnabled { get; init; }

    public bool? BackgroundCaptureOff { get; init; }

    public bool? HardwareGpuScheduling { get; init; }

    public bool? VisualEffectsOptimized { get; init; }

    public bool? MouseAccelerationOff { get; init; }

    public int? GpuDriverAgeDays { get; init; }
}
