using TweaksLabPanel.Core;

namespace TweaksLabPanel.Services;

public static class PerformanceScoreCalculator
{
    public static int Calculate(PerformanceSnapshot snapshot)
    {
        var score = 80.0;

        score -= PenalizeInstalledApps(snapshot.InstalledAppsCount);
        score -= PenalizeStartupApps(snapshot.StartupAppsCount);
        score -= PenalizeRecentErrors(snapshot.RecentErrorCount);
        score -= PenalizeCpuTemp(snapshot.CpuTempC);
        score -= PenalizeGpuTemp(snapshot.GpuTempC);
        score -= PenalizeRamUsage(snapshot.RamUsedGb, snapshot.RamTotalGb);
        score -= PenalizeTempFolder(snapshot.TempFolderSizeMb);
        score -= PenalizeProcessCount(snapshot.RunningProcessCount);
        score -= PenalizeDiskFree(snapshot.SystemDriveFreePercent);

        return (int)Math.Clamp(Math.Round(score), PerformanceScoreDefaults.MinScore, PerformanceScoreDefaults.MaxScore);
    }

    private static double PenalizeInstalledApps(int count) => count switch
    {
        > 180 => 9,
        > 140 => 7,
        > 100 => 5,
        > 70 => 3,
        > 45 => 1.5,
        _ => 0,
    };

    private static double PenalizeStartupApps(int count) => count switch
    {
        > 18 => 10,
        > 12 => 7,
        > 8 => 5,
        > 5 => 3,
        > 2 => 1,
        _ => 0,
    };

    private static double PenalizeRecentErrors(int count) => count switch
    {
        > 80 => 9,
        > 40 => 6,
        > 15 => 4,
        > 5 => 2,
        > 0 => 0.5,
        _ => 0,
    };

    private static double PenalizeCpuTemp(double tempC) => tempC switch
    {
        > 90 => 8,
        > 82 => 5,
        > 75 => 3,
        > 68 => 1,
        _ => 0,
    };

    private static double PenalizeGpuTemp(double tempC) => tempC switch
    {
        > 88 => 7,
        > 80 => 4,
        > 72 => 2,
        > 65 => 0.5,
        _ => 0,
    };

    private static double PenalizeRamUsage(double usedGb, double totalGb)
    {
        if (totalGb <= 0)
            return 0;

        var usage = usedGb / totalGb * 100;
        return usage switch
        {
            > 92 => 6,
            > 85 => 4,
            > 78 => 2,
            > 70 => 1,
            _ => 0,
        };
    }

    private static double PenalizeTempFolder(long sizeMb) => sizeMb switch
    {
        > 3000 => 5,
        > 1500 => 3,
        > 700 => 2,
        > 300 => 1,
        _ => 0,
    };

    private static double PenalizeProcessCount(int count) => count switch
    {
        > 260 => 4,
        > 210 => 3,
        > 170 => 2,
        > 130 => 1,
        _ => 0,
    };

    private static double PenalizeDiskFree(int freePercent) => freePercent switch
    {
        < 8 => 7,
        < 15 => 5,
        < 22 => 3,
        < 30 => 1,
        _ => 0,
    };
}
