using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Services;

public static class SystemPerformanceScanner
{
    public static PerformanceSnapshot Scan()
    {
        var ram = HardwareSensors.ReadRam();
        var xmp = BiosFeaturesScanner.ReadXmpState(out var xmpDetail);
        var refresh = WindowsGamingProbe.ReadRefreshRates();
        var powerPlan = WindowsGamingProbe.IsPerformancePowerPlanActive(out var powerPlanName);

        return new PerformanceSnapshot(
            InstalledAppsCount: CountInstalledApplications(),
            StartupAppsCount: CountEnabledStartupApps(),
            RecentErrorCount: CountRecentSystemErrors(),
            CpuTempC: HardwareSensors.ReadCpuTemperatureC(),
            GpuTempC: HardwareSensors.ReadGpuTemperatureC(),
            RamUsedGb: ram.UsedGb,
            RamTotalGb: ram.TotalGb,
            TempFolderSizeMb: EstimateJunkSizeMb(),
            RunningProcessCount: CountRunningProcesses(),
            SystemDriveFreePercent: ReadSystemDriveFreePercent(),
            Xmp: xmp,
            XmpDetail: xmpDetail,
            ScannedAtUtc: DateTime.UtcNow)
        {
            UptimeDays = WindowsGamingProbe.UptimeDays,
            SystemDriveIsSsd = WindowsGamingProbe.IsSystemDriveSsd(),
            RefreshRateHz = refresh?.Current,
            MaxRefreshRateHz = refresh?.Max,
            PerformancePowerPlan = powerPlan,
            PowerPlanName = powerPlanName,
            GameModeEnabled = WindowsGamingProbe.IsGameModeEnabled(),
            BackgroundCaptureOff = WindowsGamingProbe.IsBackgroundCaptureOff(),
            HardwareGpuScheduling = WindowsGamingProbe.IsHardwareGpuSchedulingEnabled(),
            VisualEffectsOptimized = WindowsGamingProbe.AreVisualEffectsOptimized(),
            MouseAccelerationOff = WindowsGamingProbe.IsMouseAccelerationOff(),
            GpuDriverAgeDays = WindowsGamingProbe.ReadGpuDriverAgeDays(),
        };
    }

    /// <summary>Applications réellement actives au démarrage (celles désactivées dans Windows ne comptent pas).</summary>
    private static int CountEnabledStartupApps()
    {
        try
        {
            return StartupAppsService.CountEnabled(StartupAppsService.Scan());
        }
        catch
        {
            return CountStartupApplications();
        }
    }

    /// <summary>Fichiers inutiles nettoyables (temporaires, rapports d'erreurs, crash dumps), corbeille non comptée.</summary>
    private static long EstimateJunkSizeMb()
    {
        try
        {
            var estimate = DiskCleanupService.Estimate();
            var bytes = estimate.Steps
                .Where(step => step.Kind != CleanupStepKind.RecycleBin)
                .Sum(step => step.Bytes);
            return bytes / (1024 * 1024);
        }
        catch
        {
            return EstimateTempFolderSizeMb();
        }
    }

    private static int CountRunningProcesses()
    {
        var processes = Process.GetProcesses();
        foreach (var process in processes)
            process.Dispose();
        return processes.Length;
    }

    private static int CountInstalledApplications()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void ScanHive(RegistryKey root, string subKey)
        {
            try
            {
                using var baseKey = root.OpenSubKey(subKey);
                if (baseKey is null)
                    return;

                foreach (var subName in baseKey.GetSubKeyNames())
                {
                    try
                    {
                        using var appKey = baseKey.OpenSubKey(subName);
                        var displayName = appKey?.GetValue("DisplayName") as string;
                        var systemComponent = appKey?.GetValue("SystemComponent");
                        var releaseType = appKey?.GetValue("ReleaseType") as string;

                        if (string.IsNullOrWhiteSpace(displayName))
                            continue;
                        if (systemComponent is int sc && sc == 1)
                            continue;
                        if (string.Equals(releaseType, "Update", StringComparison.OrdinalIgnoreCase))
                            continue;

                        names.Add(displayName.Trim());
                    }
                    catch
                    {
                        // Ignorer les clés verrouillées.
                    }
                }
            }
            catch
            {
                // Ignorer les accès refusés.
            }
        }

        const string uninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        ScanHive(Registry.LocalMachine, uninstallPath);
        ScanHive(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        ScanHive(Registry.CurrentUser, uninstallPath);

        return names.Count;
    }

    private static int CountStartupApplications()
    {
        var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void ReadRunKey(RegistryKey root, string path)
        {
            try
            {
                using var key = root.OpenSubKey(path);
                if (key is null)
                    return;

                foreach (var valueName in key.GetValueNames())
                {
                    if (!string.IsNullOrWhiteSpace(valueName))
                        entries.Add($"{root.Name}\\{valueName.Trim()}");
                }
            }
            catch
            {
                // Ignorer.
            }
        }

        ReadRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run");
        ReadRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");

        void CountStartupFolder(string folder)
        {
            try
            {
                if (!Directory.Exists(folder))
                    return;

                foreach (var file in Directory.EnumerateFileSystemEntries(folder))
                    entries.Add(Path.GetFileName(file));
            }
            catch
            {
                // Ignorer.
            }
        }

        CountStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
        CountStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));

        return entries.Count;
    }

    private static int CountRecentSystemErrors()
    {
        var count = 0;
        var since = DateTime.Now.AddDays(-7);

        foreach (var logName in new[] { "Application", "System" })
        {
            try
            {
                using var log = new EventLog(logName);
                var entries = log.Entries;
                for (var i = entries.Count - 1; i >= 0 && count < 120; i--)
                {
                    var entry = entries[i];
                    if (entry.TimeGenerated < since)
                        break;

                    if (entry.EntryType == EventLogEntryType.Error)
                        count++;
                }
            }
            catch
            {
                // Ignorer les journaux indisponibles.
            }
        }

        return count;
    }

    private static long EstimateTempFolderSizeMb()
    {
        long totalBytes = 0;

        void AddFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    return;

                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        totalBytes += new FileInfo(file).Length;
                    }
                    catch
                    {
                        // Ignorer les fichiers verrouillés.
                    }

                    if (totalBytes > 6L * 1024 * 1024 * 1024)
                        return;
                }
            }
            catch
            {
                // Ignorer.
            }
        }

        AddFolder(Path.GetTempPath());
        AddFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));

        return totalBytes / (1024 * 1024);
    }

    private static int ReadSystemDriveFreePercent()
    {
        try
        {
            var systemDrive = Environment.GetFolderPath(Environment.SpecialFolder.System).Length >= 2
                ? Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System))!
                : Path.GetPathRoot(Environment.SystemDirectory)!;

            var drive = new DriveInfo(systemDrive);
            if (!drive.IsReady || drive.TotalSize <= 0)
                return 100;

            return (int)Math.Round(drive.AvailableFreeSpace * 100.0 / drive.TotalSize);
        }
        catch
        {
            return 100;
        }
    }
}
