using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Services;

public static class SystemPerformanceScanner
{
    private const double DefaultCpuTempC = 64;
    private const double DefaultGpuTempC = 58;

    public static PerformanceSnapshot Scan()
    {
        var ram = ReadRamUsage();
        var cpuTemp = TryReadCpuTemperature() ?? DefaultCpuTempC;
        var gpuTemp = TryReadGpuTemperature() ?? DefaultGpuTempC;

        return new PerformanceSnapshot(
            InstalledAppsCount: CountInstalledApplications(),
            StartupAppsCount: CountStartupApplications(),
            RecentErrorCount: CountRecentSystemErrors(),
            CpuTempC: cpuTemp,
            GpuTempC: gpuTemp,
            RamUsedGb: ram.UsedGb,
            RamTotalGb: ram.TotalGb,
            TempFolderSizeMb: EstimateTempFolderSizeMb(),
            RunningProcessCount: Process.GetProcesses().Length,
            SystemDriveFreePercent: ReadSystemDriveFreePercent(),
            ScannedAtUtc: DateTime.UtcNow);
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

    private static (double UsedGb, double TotalGb) ReadRamUsage()
    {
        try
        {
            if (!GetPhysicallyInstalledSystemMemory(out var totalKb))
                return (0, 0);

            var totalGb = totalKb / 1024.0 / 1024.0;
            var availableKb = ReadAvailableMemoryKb();
            var usedGb = Math.Max(0, totalGb - availableKb / 1024.0 / 1024.0);
            return (Math.Round(usedGb, 1), Math.Round(totalGb, 1));
        }
        catch
        {
            return (0, 0);
        }
    }

    private static ulong ReadAvailableMemoryKb()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                if (obj["FreePhysicalMemory"] is ulong freeKb)
                    return freeKb;
            }
        }
        catch
        {
            // Ignorer.
        }

        return 0;
    }

    private static double? TryReadCpuTemperature()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["CurrentTemperature"] is not uint raw)
                    continue;

                var celsius = (raw / 10.0) - 273.15;
                if (celsius is > 20 and < 110)
                    return Math.Round(celsius, 0);
            }
        }
        catch
        {
            // Capteur indisponible sur beaucoup de PC.
        }

        return null;
    }

    private static double? TryReadGpuTemperature()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

            var temps = searcher.Get()
                .Cast<ManagementObject>()
                .Select(obj => obj["CurrentTemperature"] is uint raw ? (raw / 10.0) - 273.15 : double.NaN)
                .Where(t => t is > 20 and < 110)
                .ToList();

            if (temps.Count >= 2)
                return Math.Round(temps[^1], 0);
        }
        catch
        {
            // Ignorer.
        }

        return null;
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

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out long totalKb);
}
