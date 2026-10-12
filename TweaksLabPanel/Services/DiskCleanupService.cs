using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Services;

public enum CleanupStepKind
{
    UserTemp,
    WindowsTemp,
    ErrorReports,
    CrashDumps,
    RecycleBin,
}

public sealed record CleanupStepEstimate(CleanupStepKind Kind, long Bytes, long Items);

public sealed class CleanupEstimate
{
    public required IReadOnlyList<CleanupStepEstimate> Steps { get; init; }

    public long TotalBytes => Steps.Sum(s => s.Bytes);

    public CleanupStepEstimate For(CleanupStepKind kind) =>
        Steps.FirstOrDefault(s => s.Kind == kind) ?? new CleanupStepEstimate(kind, 0, 0);
}

/// <summary>Résultat réel d'une étape : octets effectivement supprimés et fichiers laissés en place (utilisés ou protégés).</summary>
public sealed record CleanupStepResult(CleanupStepKind Kind, long FreedBytes, int DeletedFiles, int KeptFiles, bool Skipped);

public sealed record CleanupProgress(CleanupStepKind Step, int StepIndex, int StepCount, double Percent);

/// <summary>
/// Nettoyage réel, étape par étape. Les fichiers temporaires récents (moins de 24 h) sont conservés
/// pour ne pas casser une installation ou un jeu en cours.
/// </summary>
public static class DiskCleanupService
{
    private static readonly TimeSpan RecentTempFileAge = TimeSpan.FromHours(24);
    private static readonly TimeSpan MinimumStepDuration = TimeSpan.FromMilliseconds(300);

    public static IReadOnlyList<CleanupStepKind> Steps { get; } =
    [
        CleanupStepKind.UserTemp,
        CleanupStepKind.WindowsTemp,
        CleanupStepKind.ErrorReports,
        CleanupStepKind.CrashDumps,
        CleanupStepKind.RecycleBin,
    ];

    public static string Label(CleanupStepKind kind) => kind switch
    {
        CleanupStepKind.UserTemp => LocalizationService.T("Fichiers temporaires de l’utilisateur", "User temporary files"),
        CleanupStepKind.WindowsTemp => LocalizationService.T("Fichiers temporaires de Windows", "Windows temporary files"),
        CleanupStepKind.ErrorReports => LocalizationService.T("Rapports d’erreurs Windows", "Windows error reports"),
        CleanupStepKind.CrashDumps => LocalizationService.T("Fichiers de plantage (crash dumps)", "Crash dump files"),
        CleanupStepKind.RecycleBin => LocalizationService.T("Corbeille", "Recycle Bin"),
        _ => kind.ToString(),
    };

    public static CleanupEstimate Estimate()
    {
        var steps = new List<CleanupStepEstimate>();
        foreach (var kind in Steps)
        {
            if (kind == CleanupStepKind.RecycleBin)
            {
                var (bytes, items) = QueryRecycleBin();
                steps.Add(new CleanupStepEstimate(kind, bytes, items));
                continue;
            }

            var files = ListFiles(kind);
            steps.Add(new CleanupStepEstimate(kind, files.Sum(f => f.Size), files.Count));
        }

        return new CleanupEstimate { Steps = steps };
    }

    public static IReadOnlyList<CleanupStepResult> Run(bool includeRecycleBin, IProgress<CleanupProgress>? progress)
    {
        // Plan complet d'abord, pour une barre de progression fidèle (au prorata des octets).
        var plan = Steps
            .Select(kind => (Kind: kind, Files: kind == CleanupStepKind.RecycleBin ? [] : ListFiles(kind)))
            .ToList();
        var recycle = includeRecycleBin ? QueryRecycleBin() : (Bytes: 0L, Items: 0L);

        var totalBytes = plan.Sum(p => p.Files.Sum(f => f.Size)) + recycle.Bytes;
        long processedBytes = 0;
        var results = new List<CleanupStepResult>();
        var lastReportedPercent = -1;

        // Une notification par point de pourcentage, pas une par fichier (l'interface reste fluide).
        void Report(CleanupStepKind step, int stepIndex, bool force)
        {
            var percent = Percent(processedBytes, totalBytes, stepIndex, plan.Count);
            if (!force && (int)percent == lastReportedPercent)
                return;

            lastReportedPercent = (int)percent;
            progress?.Report(new CleanupProgress(step, stepIndex, plan.Count, percent));
        }

        for (var i = 0; i < plan.Count; i++)
        {
            var (kind, files) = plan[i];
            var stepWatch = Stopwatch.StartNew();
            Report(kind, i, force: true);

            if (kind == CleanupStepKind.RecycleBin)
            {
                if (!includeRecycleBin)
                {
                    results.Add(new CleanupStepResult(kind, 0, 0, 0, Skipped: true));
                }
                else
                {
                    var emptied = recycle.Items == 0 || EmptyRecycleBin();
                    results.Add(new CleanupStepResult(kind, emptied ? recycle.Bytes : 0, emptied ? (int)recycle.Items : 0,
                        emptied ? 0 : (int)recycle.Items, Skipped: false));
                    processedBytes += recycle.Bytes;
                }
            }
            else
            {
                long freed = 0;
                int deleted = 0, kept = 0;
                foreach (var file in files)
                {
                    switch (TryDeleteFile(file.Path))
                    {
                        case DeleteOutcome.Deleted:
                            freed += file.Size;
                            deleted++;
                            break;
                        case DeleteOutcome.Kept:
                            kept++;
                            break;
                    }

                    processedBytes += file.Size;
                    Report(kind, i, force: false);
                }

                RemoveEmptyDirectories(kind);
                results.Add(new CleanupStepResult(kind, freed, deleted, kept, Skipped: false));
            }

            // Laisse le temps de lire chaque étape à l'écran (le travail, lui, est déjà fait).
            var remaining = MinimumStepDuration - stepWatch.Elapsed;
            if (remaining > TimeSpan.Zero)
                Thread.Sleep(remaining);
        }

        progress?.Report(new CleanupProgress(plan[^1].Kind, plan.Count, plan.Count, 100));
        return results;
    }

    private static double Percent(long processedBytes, long totalBytes, int stepIndex, int stepCount) =>
        totalBytes > 0
            ? Math.Clamp(processedBytes * 100.0 / totalBytes, 0, 100)
            : stepIndex * 100.0 / stepCount;

    private static IEnumerable<string> FoldersFor(CleanupStepKind kind)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        var userTemp = Path.GetTempPath();
        var windowsTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");

        return kind switch
        {
            CleanupStepKind.UserTemp => [userTemp],
            // Si TEMP pointe déjà vers C:\Windows\Temp, on ne le compte pas deux fois.
            CleanupStepKind.WindowsTemp => SamePath(userTemp, windowsTemp) ? [] : [windowsTemp],
            CleanupStepKind.ErrorReports =>
            [
                Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportArchive"),
                Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportQueue"),
                Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportArchive"),
                Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportQueue"),
            ],
            CleanupStepKind.CrashDumps => [Path.Combine(localAppData, "CrashDumps")],
            _ => [],
        };
    }

    private static bool KeepsRecentFiles(CleanupStepKind kind) =>
        kind is CleanupStepKind.UserTemp or CleanupStepKind.WindowsTemp;

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(a).TrimEnd('\\', '/'),
                Path.GetFullPath(b).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sous-dossiers jamais nettoyés : « .net » contient les DLL extraites par les applis .NET en un seul fichier
    /// (dont le panel lui-même une fois publié).
    /// </summary>
    private static bool IsProtectedPath(CleanupStepKind kind, string folder, string path)
    {
        if (!KeepsRecentFiles(kind))
            return false;

        var protectedRoot = Path.Combine(folder, ".net") + Path.DirectorySeparatorChar;
        return path.StartsWith(protectedRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path.TrimEnd('\\', '/'), protectedRoot.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Date la plus récente entre création et modification (un fichier extrait garde souvent une vieille date de modification).</summary>
    private static DateTime NewestTimestampUtc(FileSystemInfo info) =>
        info.CreationTimeUtc > info.LastWriteTimeUtc ? info.CreationTimeUtc : info.LastWriteTimeUtc;

    /// <summary>
    /// Garde-fou : un dossier temporaire n'est nettoyé que s'il s'appelle vraiment « Temp » / « Tmp »
    /// et n'est pas la racine d'un disque (variable TEMP mal configurée = on ne touche à rien).
    /// </summary>
    private static bool IsSafeCleanupFolder(CleanupStepKind kind, string folder)
    {
        try
        {
            var full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrEmpty(full) || Path.GetPathRoot(full)?.TrimEnd('\\', '/') == full)
                return false;

            if (!KeepsRecentFiles(kind))
                return true;

            var name = Path.GetFileName(full);
            return name.Equals("Temp", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Tmp", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static List<(string Path, long Size)> ListFiles(CleanupStepKind kind)
    {
        var result = new List<(string, long)>();
        var cutoff = DateTime.UtcNow - RecentTempFileAge;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
        };

        foreach (var folder in FoldersFor(kind).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(folder) || !IsSafeCleanupFolder(kind, folder))
                continue;

            try
            {
                foreach (var path in Directory.EnumerateFiles(folder, "*", options))
                {
                    try
                    {
                        if (IsProtectedPath(kind, folder, path))
                            continue;

                        var info = new FileInfo(path);
                        if (KeepsRecentFiles(kind) && NewestTimestampUtc(info) > cutoff)
                            continue;

                        result.Add((path, info.Length));
                    }
                    catch
                    {
                        // Fichier disparu ou illisible.
                    }
                }
            }
            catch
            {
                // Dossier inaccessible sans droits administrateur.
            }
        }

        return result;
    }

    private enum DeleteOutcome
    {
        Deleted,
        Kept,
        AlreadyGone,
    }

    private static DeleteOutcome TryDeleteFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return DeleteOutcome.AlreadyGone;

            if (info.IsReadOnly)
                info.IsReadOnly = false;

            info.Delete();
            return DeleteOutcome.Deleted;
        }
        catch
        {
            // Fichier utilisé par une application ou protégé : on le laisse.
            return DeleteOutcome.Kept;
        }
    }

    private static void RemoveEmptyDirectories(CleanupStepKind kind)
    {
        foreach (var folder in FoldersFor(kind))
        {
            if (!Directory.Exists(folder) || !IsSafeCleanupFolder(kind, folder))
                continue;

            try
            {
                var directories = Directory.EnumerateDirectories(folder, "*", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        AttributesToSkip = FileAttributes.ReparsePoint,
                    })
                    .OrderByDescending(d => d.Length)
                    .ToList();
                var cutoff = DateTime.UtcNow - RecentTempFileAge;

                foreach (var directory in directories)
                {
                    try
                    {
                        if (IsProtectedPath(kind, folder, directory))
                            continue;

                        // Dossier de travail créé récemment par une appli en cours : on le laisse.
                        if (KeepsRecentFiles(kind) && NewestTimestampUtc(new DirectoryInfo(directory)) > cutoff)
                            continue;

                        if (!Directory.EnumerateFileSystemEntries(directory).Any())
                            Directory.Delete(directory);
                    }
                    catch
                    {
                        // Dossier utilisé : on le laisse.
                    }
                }
            }
            catch
            {
                // Ignorer.
            }
        }
    }

    private static (long Bytes, long Items) QueryRecycleBin()
    {
        try
        {
            var info = new ShQueryRbInfo { Size = Marshal.SizeOf<ShQueryRbInfo>() };
            if (SHQueryRecycleBin(null, ref info) == 0)
                return (Math.Max(0, info.TotalBytes), Math.Max(0, info.ItemCount));
        }
        catch
        {
            // Ignorer.
        }

        return (0, 0);
    }

    private static bool EmptyRecycleBin()
    {
        try
        {
            return SHEmptyRecycleBin(IntPtr.Zero, null, SherbNoConfirmation | SherbNoProgressUi | SherbNoSound) == 0;
        }
        catch
        {
            return false;
        }
    }

    private const uint SherbNoConfirmation = 0x00000001;
    private const uint SherbNoProgressUi = 0x00000002;
    private const uint SherbNoSound = 0x00000004;

    [StructLayout(LayoutKind.Sequential)]
    private struct ShQueryRbInfo
    {
        public int Size;
        public long TotalBytes;
        public long ItemCount;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? rootPath, ref ShQueryRbInfo info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);
}
