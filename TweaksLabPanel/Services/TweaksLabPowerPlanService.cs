using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Services;

public sealed class PowerPlanResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public static class TweaksLabPowerPlanService
{
    public const string PlanDisplayName = "Free Tweaks Lab";

    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff1bb2600002";

    private static readonly string[] PlanSearchNames =
    [
        "Free Tweaks Lab",
        "TweaksLab Community",
        "Tweaks Lab - Power Plan",
    ];

    private static readonly Regex GuidRegex = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    public static bool IsInstalled()
    {
        var guid = TweaksLabPowerPlanState.PlanGuid;
        if (!string.IsNullOrWhiteSpace(guid) && PlanExists(guid))
            return TweaksLabPowerPlanState.Installed;

        var existing = FindPlanGuidByName();
        if (existing is null)
            return false;

        SaveInstalled(existing);
        return true;
    }

    public static bool IsActive()
    {
        var guid = TweaksLabPowerPlanState.PlanGuid ?? FindPlanGuidByName();
        if (string.IsNullOrWhiteSpace(guid))
            return false;

        return string.Equals(GetActiveGuid(), guid, StringComparison.OrdinalIgnoreCase);
    }

    public static Task<PowerPlanResult> InstallAsync() => Task.Run(Install);

    public static Task<PowerPlanResult> ActivateAsync() => Task.Run(Activate);

    public static Task<PowerPlanResult> DeactivateAsync() => Task.Run(Deactivate);

    private static PowerPlanResult Install()
    {
        try
        {
            CaptureFallbackBeforeActivation();

            var existingGuid = ResolveInstalledPlanGuid();
            if (existingGuid is not null)
            {
                RemoveDuplicatePlans(existingGuid);
                SaveInstalled(existingGuid);
                RunPowerCfg($"/setactive {existingGuid}");
                if (!IsActive())
                    return Fail("Plan déjà installé mais impossible de l'activer.");

                return Ok();
            }

            var powPath = AssetHelper.PowerPlan;
            if (!File.Exists(powPath))
                return Fail("Fichier du plan d'alimentation introuvable.");

            var output = RunPowerCfg($"/import \"{powPath}\"");
            var importedGuid = ExtractGuid(output);
            if (string.IsNullOrWhiteSpace(importedGuid))
            {
                importedGuid = FindPlanGuidByName();
                if (importedGuid is null)
                    return Fail("Impossible d'installer le plan d'alimentation.");
            }

            SaveInstalled(importedGuid);
            TweaksLabPowerPlanState.ExportPath = powPath;
            RemoveDuplicatePlans(importedGuid);

            RunPowerCfg($"/setactive {importedGuid}");
            if (!IsActive())
                return Fail("Plan importé mais impossible de l'activer.");

            return Ok();
        }
        catch
        {
            return Fail("Impossible d'installer le plan d'alimentation.");
        }
    }

    private static PowerPlanResult Activate()
    {
        try
        {
            if (!IsInstalled())
                return Fail("Installe d'abord le plan TweaksLab Community.");

            var planGuid = TweaksLabPowerPlanState.PlanGuid ?? FindPlanGuidByName();
            if (string.IsNullOrWhiteSpace(planGuid))
                return Fail("Installe d'abord le plan TweaksLab Community.");

            CaptureFallbackBeforeActivation();

            RunPowerCfg($"/setactive {planGuid}");
            if (!IsActive())
                return Fail("Impossible d'activer le plan.");

            return Ok();
        }
        catch
        {
            return Fail("Impossible d'activer le plan.");
        }
    }

    private static PowerPlanResult Deactivate()
    {
        try
        {
            var restore = ResolveFallbackGuid();
            RunPowerCfg($"/setactive {restore}");

            if (IsActive())
                return Fail("Impossible de désactiver le plan.");

            return Ok();
        }
        catch
        {
            return Fail("Impossible de désactiver le plan.");
        }
    }

    private static void CaptureFallbackBeforeActivation()
    {
        var tweaksLabGuid = TweaksLabPowerPlanState.PlanGuid ?? FindPlanGuidByName();
        var active = GetActiveGuid();
        if (string.IsNullOrWhiteSpace(active))
            return;

        if (!string.IsNullOrWhiteSpace(tweaksLabGuid)
            && string.Equals(active, tweaksLabGuid, StringComparison.OrdinalIgnoreCase))
            return;

        TweaksLabPowerPlanState.FallbackGuid = active;
    }

    private static string ResolveFallbackGuid()
    {
        var fallback = TweaksLabPowerPlanState.FallbackGuid;
        if (!string.IsNullOrWhiteSpace(fallback) && PlanExists(fallback))
            return fallback;

        return BalancedGuid;
    }

    private static void SaveInstalled(string guid)
    {
        TweaksLabPowerPlanState.PlanGuid = guid.ToLowerInvariant();
        TweaksLabPowerPlanState.Installed = true;
    }

    private static bool PlanExists(string guid)
        => RunPowerCfg("/list").Contains(guid, StringComparison.OrdinalIgnoreCase);

    private static string? ResolveInstalledPlanGuid()
    {
        var guid = TweaksLabPowerPlanState.PlanGuid;
        if (!string.IsNullOrWhiteSpace(guid) && PlanExists(guid))
            return guid.ToLowerInvariant();

        return FindPlanGuidByName();
    }

    private static void RemoveDuplicatePlans(string keepGuid)
    {
        foreach (var guid in FindAllPlanGuidsByName())
        {
            if (string.Equals(guid, keepGuid, StringComparison.OrdinalIgnoreCase))
                continue;

            RunPowerCfg($"/delete {guid}");
        }
    }

    private static List<string> FindAllPlanGuidsByName()
    {
        var results = new List<string>();
        foreach (var line in RunPowerCfg("/list").Split('\n'))
        {
            if (!PlanSearchNames.Any(name => line.Contains(name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var match = GuidRegex.Match(line);
            if (match.Success)
                results.Add(match.Value.ToLowerInvariant());
        }

        return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? FindPlanGuidByName()
    {
        foreach (var line in RunPowerCfg("/list").Split('\n'))
        {
            if (!PlanSearchNames.Any(name => line.Contains(name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var match = GuidRegex.Match(line);
            if (match.Success)
                return match.Value.ToLowerInvariant();
        }

        return null;
    }

    private static string? GetActiveGuid()
        => ExtractGuid(RunPowerCfg("/getactivescheme"))?.ToLowerInvariant();

    private static string? ExtractGuid(string text)
    {
        var match = GuidRegex.Match(text);
        return match.Success ? match.Value : null;
    }

    private static string RunPowerCfg(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powercfg",
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(psi);
        if (process is null)
            return string.Empty;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(30000);
        return output;
    }

    private static PowerPlanResult Ok() => new() { Success = true };

    private static PowerPlanResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
