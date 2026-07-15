using System.IO;
using System.Management;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Services;

public static class SystemSpecsScanner
{
    public static SystemSpecsSnapshot Scan()
    {
        var perf = SystemPerformanceScanner.Scan();
        var os = ReadOperatingSystem();
        var cpu = ReadProcessor();
        var gpu = ReadGraphicsCard();
        var ram = ReadMemory();
        var board = ReadMotherboard();
        var storage = ReadStorage();
        var drive = ReadSystemDrive();

        return new SystemSpecsSnapshot(
            OperatingSystem: os,
            Processor: cpu.Name,
            ProcessorDetails: cpu.Details,
            GraphicsCard: gpu,
            Memory: ram,
            Motherboard: board,
            Storage: storage,
            CpuTempC: perf.CpuTempC,
            GpuTempC: perf.GpuTempC,
            SystemDriveFreePercent: drive.FreePercent,
            SystemDriveFreeBytes: drive.FreeBytes,
            SystemDriveTotalBytes: drive.TotalBytes,
            SystemDriveLetter: drive.Letter);
    }

    public static string FormatSpecsText(SystemSpecsSnapshot s) =>
        $"""
        Système d'exploitation : {s.OperatingSystem}
        Processeur : {s.Processor} ({s.ProcessorDetails})
        Carte graphique : {s.GraphicsCard}
        Mémoire vive : {s.Memory}
        Carte mère : {s.Motherboard}
        Stockage : {s.Storage}
        """;

    private static string ReadOperatingSystem()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var caption = obj["Caption"]?.ToString()?.Trim() ?? "Windows";
                var version = obj["Version"]?.ToString()?.Trim();
                return string.IsNullOrWhiteSpace(version) ? caption : $"{caption} ({version})";
            }
        }
        catch { }

        return Environment.OSVersion.VersionString;
    }

    private static (string Name, string Details) ReadProcessor()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = CleanHardwareName(obj["Name"]?.ToString() ?? "Processeur");
                var cores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                var threads = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                var details = cores > 0 && threads > 0 ? $"{cores}c/{threads}t" : "—";
                return (name, details);
            }
        }
        catch { }

        return ("Processeur", "—");
    }

    private static string ReadGraphicsCard()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterRAM FROM Win32_VideoController");
            string? best = null;
            long bestRam = 0;

            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name) || name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase))
                    continue;

                long ram = 0;
                try { ram = Convert.ToInt64(obj["AdapterRAM"] ?? 0); } catch { }

                if (ram >= bestRam)
                {
                    bestRam = ram;
                    best = CleanHardwareName(name);
                }
            }

            if (!string.IsNullOrWhiteSpace(best))
                return best;
        }
        catch { }

        return "Carte graphique non détectée";
    }

    private static string ReadMemory()
    {
        try
        {
            double totalGb = 0;
            int? speed = null;

            using var searcher = new ManagementObjectSearcher(
                "SELECT Capacity, Speed FROM Win32_PhysicalMemory");
            foreach (ManagementObject obj in searcher.Get())
            {
                totalGb += Convert.ToInt64(obj["Capacity"] ?? 0) / (1024d * 1024 * 1024);
                if (obj["Speed"] is uint sp && sp > 0)
                    speed = (int)sp;
            }

            if (totalGb > 0)
            {
                var text = $"{totalGb:0.#} GB";
                if (speed is > 0)
                    text += $" @ {speed} MHz";
                return text;
            }
        }
        catch { }

        return "—";
    }

    private static string ReadMotherboard()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Manufacturer, Product FROM Win32_BaseBoard");
            foreach (ManagementObject obj in searcher.Get())
            {
                var manufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? "";
                var product = obj["Product"]?.ToString()?.Trim() ?? "";
                var combined = $"{manufacturer} {product}".Trim();
                return string.IsNullOrWhiteSpace(combined) ? "—" : combined;
            }
        }
        catch { }

        return "—";
    }

    private static string ReadStorage()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Model, Size FROM Win32_DiskDrive");
            foreach (ManagementObject obj in searcher.Get())
            {
                var model = obj["Model"]?.ToString()?.Trim() ?? "Disque";
                var sizeBytes = Convert.ToInt64(obj["Size"] ?? 0);
                if (sizeBytes <= 0)
                    return model;

                var sizeGb = sizeBytes / (1024d * 1024 * 1024);
                return $"{model} — {sizeGb:0.#} GB";
            }
        }
        catch { }

        return "—";
    }

    private static (string Letter, long FreeBytes, long TotalBytes, double FreePercent) ReadSystemDrive()
    {
        try
        {
            var letter = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(letter);
            if (!drive.IsReady)
                return (letter.TrimEnd('\\'), 0, 0, 0);

            var total = drive.TotalSize;
            var free = drive.AvailableFreeSpace;
            var percent = total > 0 ? free * 100d / total : 0;
            return (drive.Name.TrimEnd('\\'), free, total, percent);
        }
        catch { }

        return ("C:", 0, 0, 0);
    }

    private static string CleanHardwareName(string value) =>
        value.Replace("(R)", "", StringComparison.OrdinalIgnoreCase)
            .Replace("(TM)", "", StringComparison.OrdinalIgnoreCase)
            .Replace("  ", " ")
            .Trim();
}
