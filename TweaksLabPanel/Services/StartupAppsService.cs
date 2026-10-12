using System.IO;
using Microsoft.Win32;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Services;

public sealed class StartupToggleResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public static class StartupAppsService
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRunPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private static readonly byte[] DisabledApproval = [0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] EnabledApproval = [0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    private static readonly string DisabledUserStartupDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TweaksLabPanel",
        "disabled-startup",
        "user");

    public static IReadOnlyList<StartupAppEntry> Scan()
    {
        var entries = new List<StartupAppEntry>();
        ScanRunHive(Registry.CurrentUser, StartupEntrySource.UserRun, canToggle: true, entries);
        ScanRunHive(Registry.LocalMachine, StartupEntrySource.MachineRun, canToggle: true, entries);
        ScanStartupFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            StartupEntrySource.UserStartupFolder,
            canToggle: true,
            entries);
        ScanStartupFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            StartupEntrySource.CommonStartupFolder,
            canToggle: false,
            entries);

        return entries
            .OrderByDescending(e => e.IsEnabled)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static int CountEnabled(IEnumerable<StartupAppEntry> entries)
        => entries.Count(e => e.IsEnabled);

    public static StartupToggleResult SetEnabled(StartupAppEntry entry, bool enabled)
    {
        try
        {
            if (!entry.CanToggle)
                return Fail(LocalizationService.T(
                    "Cette application ne peut pas être modifiée depuis le panel.",
                    "This app can't be changed from the panel."));

            return entry.Source switch
            {
                StartupEntrySource.UserRun => SetRunApproved(Registry.CurrentUser, entry.Name, enabled)
                    ? Ok()
                    : Fail(LocalizationService.T(
                        "Impossible de modifier cette application.",
                        "Couldn't change this app.")),
                StartupEntrySource.MachineRun => SetRunApproved(Registry.LocalMachine, entry.Name, enabled)
                    ? Ok()
                    : Fail(LocalizationService.T(
                        "Droits administrateur requis pour modifier cette application.",
                        "Administrator rights are required to change this app.")),
                StartupEntrySource.UserStartupFolder => SetUserStartupFileEnabled(entry.Name, enabled),
                _ => Fail(LocalizationService.T(
                    "Cette application ne peut pas être modifiée depuis le panel.",
                    "This app can't be changed from the panel.")),
            };
        }
        catch
        {
            return Fail(LocalizationService.T(
                "Impossible de modifier cette application.",
                "Couldn't change this app."));
        }
    }

    private static void ScanRunHive(
        RegistryKey root,
        StartupEntrySource source,
        bool canToggle,
        ICollection<StartupAppEntry> entries)
    {
        try
        {
            using var run = root.OpenSubKey(RunPath, writable: false);
            if (run is null)
                return;

            using var approved = root.OpenSubKey(ApprovedRunPath, writable: false);
            foreach (var valueName in run.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(valueName))
                    continue;

                var command = run.GetValue(valueName)?.ToString() ?? string.Empty;
                entries.Add(new StartupAppEntry
                {
                    Id = $"{source}:{valueName}",
                    Name = BeautifyName(valueName),
                    Detail = ShortenCommand(command),
                    Source = source,
                    IsEnabled = IsStartupEntryEnabled(approved, valueName),
                    CanToggle = canToggle,
                });
            }
        }
        catch
        {
            // Ignorer.
        }
    }

    private static void ScanStartupFolder(
        string folder,
        StartupEntrySource source,
        bool canToggle,
        ICollection<StartupAppEntry> entries)
    {
        try
        {
            if (!Directory.Exists(folder))
                return;

            foreach (var file in Directory.EnumerateFiles(folder))
            {
                var ext = Path.GetExtension(file);
                if (!IsStartupExtension(ext))
                    continue;

                var fileName = Path.GetFileName(file);
                entries.Add(new StartupAppEntry
                {
                    Id = $"{source}:{fileName}",
                    Name = Path.GetFileNameWithoutExtension(fileName),
                    Detail = folder,
                    Source = source,
                    IsEnabled = true,
                    CanToggle = canToggle,
                });
            }

            if (source != StartupEntrySource.UserStartupFolder || !Directory.Exists(DisabledUserStartupDir))
                return;

            foreach (var file in Directory.EnumerateFiles(DisabledUserStartupDir))
            {
                var ext = Path.GetExtension(file);
                if (!IsStartupExtension(ext))
                    continue;

                var fileName = Path.GetFileName(file);
                entries.Add(new StartupAppEntry
                {
                    Id = $"{source}:{fileName}",
                    Name = Path.GetFileNameWithoutExtension(fileName),
                    Detail = LocalizationService.T("Désactivé via TweaksLab Panel", "Disabled via TweaksLab Panel"),
                    Source = source,
                    IsEnabled = false,
                    CanToggle = canToggle,
                });
            }
        }
        catch
        {
            // Ignorer.
        }
    }

    private static bool SetRunApproved(RegistryKey root, string name, bool enabled)
    {
        using var approved = root.CreateSubKey(ApprovedRunPath, true);
        if (approved is null)
            return false;

        approved.SetValue(name, enabled ? EnabledApproval : DisabledApproval, RegistryValueKind.Binary);
        return true;
    }

    private static StartupToggleResult SetUserStartupFileEnabled(string fileName, bool enabled)
    {
        var startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var livePath = Path.Combine(startupDir, fileName);
        var disabledPath = Path.Combine(DisabledUserStartupDir, fileName);

        Directory.CreateDirectory(DisabledUserStartupDir);

        if (enabled)
        {
            if (!File.Exists(disabledPath))
                return Fail(LocalizationService.T("Fichier introuvable.", "File not found."));

            if (File.Exists(livePath))
                File.Delete(livePath);

            File.Move(disabledPath, livePath);
            return Ok();
        }

        if (!File.Exists(livePath))
            return Fail(LocalizationService.T("Fichier introuvable.", "File not found."));

        if (File.Exists(disabledPath))
            File.Delete(disabledPath);

        File.Move(livePath, disabledPath);
        return Ok();
    }

    private static bool IsStartupEntryEnabled(RegistryKey? approved, string name)
    {
        if (approved is null)
            return true;

        if (approved.GetValue(name) is not byte[] data || data.Length == 0)
            return true;

        return data[0] != 0x03;
    }

    private static bool IsStartupExtension(string ext) =>
        ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".bat", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase);

    private static string BeautifyName(string raw) =>
        raw.Replace('_', ' ').Trim();

    private static string ShortenCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return string.Empty;

        const int max = 72;
        command = command.Trim();
        return command.Length <= max ? command : command[..(max - 3)] + "...";
    }

    private static StartupToggleResult Ok() => new() { Success = true };

    private static StartupToggleResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
