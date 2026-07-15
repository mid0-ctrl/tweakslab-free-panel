namespace TweaksLabPanel.Core;

public sealed record PerformanceSnapshot(
    int InstalledAppsCount,
    int StartupAppsCount,
    int RecentErrorCount,
    double CpuTempC,
    double GpuTempC,
    double RamUsedGb,
    double RamTotalGb,
    long TempFolderSizeMb,
    int RunningProcessCount,
    int SystemDriveFreePercent,
    DateTime ScannedAtUtc);
