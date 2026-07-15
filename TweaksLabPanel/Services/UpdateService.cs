using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Services;

public static class UpdateService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(10),
        DefaultRequestHeaders = { { "User-Agent", "TweaksLab-Panel-Update" } },
    };

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private static bool _checkRunning;

    public static Version CurrentVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

    public static void QueueStartupCheck()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await CheckAndPromptAsync();
            }
            catch (Exception ex)
            {
                StartupLog.Write("Update check: " + ex.Message);
            }
        });
    }

    private static async Task CheckAndPromptAsync()
    {
        if (_checkRunning)
            return;

        _checkRunning = true;
        try
        {
            if (DateTime.UtcNow - AppSettingsStore.Instance.LastUpdateCheckUtc < CheckInterval)
                return;

            AppSettingsStore.Instance.MarkUpdateChecked();

            var release = await FetchLatestReleaseAsync();
            if (release is null)
                return;

            if (!TryParseVersion(release.TagName, out var remoteVersion))
                return;

            if (remoteVersion <= CurrentVersion)
                return;

            if (string.Equals(AppSettingsStore.Instance.SkippedUpdateVersion, release.TagName, StringComparison.OrdinalIgnoreCase))
                return;

            if (string.IsNullOrWhiteSpace(release.DownloadUrl))
                return;

            await Application.Current.Dispatcher.InvokeAsync(() => PromptUpdate(release, remoteVersion));
        }
        finally
        {
            _checkRunning = false;
        }
    }

    private static void PromptUpdate(ReleaseInfo release, Version remoteVersion)
    {
        var message = LocalizationService.UpdateAvailableMessage(
            remoteVersion.ToString(3),
            string.IsNullOrWhiteSpace(release.Notes) ? LocalizationService.UpdateDefaultNotes : release.Notes.Trim());

        var result = MessageBox.Show(
            message,
            LocalizationService.UpdateAvailableTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result != MessageBoxResult.Yes)
        {
            AppSettingsStore.Instance.SkipUpdateVersion(release.TagName);
            return;
        }

        _ = Task.Run(() => DownloadAndApplyAsync(release));
    }

    private static async Task DownloadAndApplyAsync(ReleaseInfo release)
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TweaksLabPanel",
                "updates");
            Directory.CreateDirectory(root);

            var zipPath = Path.Combine(root, "pending.zip");
            var extractDir = Path.Combine(root, "pending-extract");
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);

            await using (var input = await Http.GetStreamAsync(release.DownloadUrl))
            await using (var output = File.Create(zipPath))
                await input.CopyToAsync(output);

            ZipFile.ExtractToDirectory(zipPath, extractDir, true);

            var installDir = Path.GetDirectoryName(Environment.ProcessPath)
                ?? AppContext.BaseDirectory;

            var updaterPath = Path.Combine(root, "apply-update.cmd");
            var script = $"""
                @echo off
                timeout /t 2 /nobreak > nul
                xcopy /Y /E /I "{extractDir}\*" "{installDir}\"
                start "" "{Path.Combine(installDir, "TweaksLabPanel.exe")}"
                del "%~f0"
                """;

            await File.WriteAllTextAsync(updaterPath, script);

            Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                UseShellExecute = true,
                CreateNoWindow = true,
            });

            await Application.Current.Dispatcher.InvokeAsync(() => Application.Current.Shutdown());
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
                MessageBox.Show(
                    LocalizationService.UpdateFailed(ex.Message),
                    LocalizationService.UpdateAvailableTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning));
        }
    }

    private static async Task<ReleaseInfo?> FetchLatestReleaseAsync()
    {
        await using var stream = await Http.GetStreamAsync(PanelEndpoints.GitHubLatestReleaseApi);
        var payload = await JsonSerializer.DeserializeAsync<GitHubReleaseResponse>(stream);
        if (payload is null)
            return null;

        var zipAsset = payload.Assets?
            .FirstOrDefault(a => a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true);

        return new ReleaseInfo(
            payload.TagName ?? "",
            zipAsset?.BrowserDownloadUrl ?? "",
            payload.Body ?? "");
    }

    private static bool TryParseVersion(string tag, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var cleaned = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(cleaned, out version!);
    }

    private sealed record ReleaseInfo(string TagName, string DownloadUrl, string Notes);

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset>? Assets { get; set; }
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
