using System.IO;

namespace TweaksLabPanel.Services;

public sealed class CleanupEstimate
{
    public long TempUserBytes { get; init; }
    public long TempWindowsBytes { get; init; }
    public long RecycleBinBytes { get; init; }

    public long TotalBytes => TempUserBytes + TempWindowsBytes + RecycleBinBytes;
    public double TotalGb => TotalBytes / (1024d * 1024 * 1024);
}

public static class DiskCleanupScanner
{
    public static CleanupEstimate Scan()
    {
        return new CleanupEstimate
        {
            TempUserBytes = SafeDirSize(Path.GetTempPath()),
            TempWindowsBytes = SafeDirSize(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")),
            RecycleBinBytes = EstimateRecycleBinBytes(),
        };
    }

    public static long SafeDirSize(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch { }
            }
        }
        catch { }

        return total;
    }

    private static long EstimateRecycleBinBytes()
    {
        try
        {
            long total = 0;
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var recycle = Path.Combine(drive.Name, "$Recycle.Bin");
                total += SafeDirSize(recycle);
            }
            return total;
        }
        catch
        {
            return 0;
        }
    }
}

public static class DiskCleanupService
{
    public static CleanupEstimate CleanAll()
    {
        var before = DiskCleanupScanner.Scan();
        CleanTournament();
        var after = DiskCleanupScanner.Scan();

        return new CleanupEstimate
        {
            TempUserBytes = Math.Max(0, before.TempUserBytes - after.TempUserBytes),
            TempWindowsBytes = Math.Max(0, before.TempWindowsBytes - after.TempWindowsBytes),
            RecycleBinBytes = Math.Max(0, before.RecycleBinBytes - after.RecycleBinBytes),
        };
    }

    public static void CleanTournament()
    {
        EmptyRecycleBin();
        TryDeleteDirectoryContents(Path.GetTempPath());
        TryDeleteDirectoryContents(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
    }

    private static void EmptyRecycleBin()
    {
        try
        {
            Shell32.SHEmptyRecycleBin(IntPtr.Zero, null!, Shell32.SherbNoConfirmation | Shell32.SherbNoSound);
        }
        catch { }
    }

    private static void TryDeleteDirectoryContents(string path)
    {
        if (!Directory.Exists(path))
            return;

        foreach (var file in Directory.EnumerateFiles(path))
        {
            try { File.Delete(file); } catch { }
        }

        foreach (var dir in Directory.EnumerateDirectories(path))
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static class Shell32
    {
        public const uint SherbNoConfirmation = 0x00000001;
        public const uint SherbNoSound = 0x00000004;

        [System.Runtime.InteropServices.DllImport("Shell32.dll")]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);
    }
}
