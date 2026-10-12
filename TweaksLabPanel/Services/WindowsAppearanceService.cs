using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Services;

public sealed class AppearanceTweakResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Profil "Paramètres personnalisés" des Options de performances Windows.
/// Activé : 3 cases cochées (miniatures, contenu fenêtres, lissage polices).
/// Désactivé : toutes les cases cochées.
/// </summary>
public static class WindowsAppearanceService
{
    private const string LogCategory = "Appearance";
    private const string VisualEffectsKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";
    private const string ExplorerAdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string DesktopKey = @"Control Panel\Desktop";

    // Masque minimal : les 3 effets viennent des clés Advanced/Desktop.
    private static readonly byte[] OptimizedMask = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    // Masque "meilleure apparence" Windows (état restauré par défaut).
    private static readonly byte[] BestAppearanceMask = [0x9E, 0x3E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00];

    private const ulong OptimizedVisualBits = 0;

    private const int SpiGetUiEffects = 0x103E;
    private const int SpiGetClientAreaAnimation = 0x1041;
    private const int SpiGetMenuAnimation = 0x1002;
    private const int SpiGetComboBoxAnimation = 0x1004;
    private const int SpiGetListBoxSmoothScrolling = 0x1006;
    private const int SpiGetSelectionFade = 0x1013;
    private const int SpiGetTooltipAnimation = 0x1016;
    private const int SpiGetTooltipFade = 0x1019;
    private const int SpiGetCursorShadow = 0x101A;
    private const int SpiGetDragFullWindows = 0x0026;
    private const int SpiGetFontSmoothing = 0x004C;
    private const int SpiGetAnimation = 0x0048;

    private const int SpiSetAnimation = 0x0049;
    private const int SpiSetDragFullWindows = 0x0025;
    private const int SpiSetFontSmoothing = 0x004B;
    private const int SpiSetMenuAnimation = 0x1003;
    private const int SpiSetComboBoxAnimation = 0x1005;
    private const int SpiSetListBoxSmoothScrolling = 0x1007;
    private const int SpiSetSelectionFade = 0x1014;
    private const int SpiSetTooltipAnimation = 0x1017;
    private const int SpiSetTooltipFade = 0x1018;
    private const int SpiSetCursorShadow = 0x101B;
    private const int SpiSetUiEffects = 0x103F;
    private const int SpiSetClientAreaAnimation = 0x1042;
    private const int SpiFUpdateIniFile = 0x01;
    private const int SpiFSendChange = 0x02;
    private const int HwndBroadcast = 0xffff;
    private const int WmSettingChange = 0x001A;

    public static string LogFilePath => DiagnosticLog.CurrentLogPath;
    public static string LogDirectoryPath => DiagnosticLog.LogDirectoryPath;

    public static bool IsActive()
    {
        if (!WindowsAppearanceState.Active)
            return false;

        return MatchesOptimizedProfile();
    }

    public static void SyncStoredState()
    {
        var storedActive = WindowsAppearanceState.Active;
        var matchesOptimized = MatchesOptimizedProfile();
        var matchesFull = MatchesFullAppearanceProfile();

        if (storedActive && !matchesOptimized)
        {
            DiagnosticLog.Write(LogCategory,
                $"SyncStoredState: état stocké Actif=true mais registre non optimisé. " +
                $"optimized={matchesOptimized} full={matchesFull} {DescribeRegistrySnapshot()}");
            WindowsAppearanceState.Active = false;
        }
    }

    public static Task<AppearanceTweakResult> EnableAsync() => Task.Run(Enable);

    public static Task<AppearanceTweakResult> DisableAsync() => Task.Run(Disable);

    private static AppearanceTweakResult Enable()
    {
        DiagnosticLog.Write(LogCategory, $"Enable démarré (thread={Environment.CurrentManagedThreadId})");
        LogRegistrySnapshot("Enable avant application");

        try
        {
            EnsureRestoredProfileCaptured();
            ApplyOptimized("Enable tentative 1");
            if (!MatchesOptimizedProfile(out var mismatch1))
            {
                DiagnosticLog.Write(LogCategory, $"Enable: vérification échouée après tentative 1. {mismatch1}");
                ApplyOptimized("Enable tentative 2");
                Thread.Sleep(250);
            }

            if (!MatchesOptimizedProfile(out var mismatch2))
            {
                DiagnosticLog.Write(LogCategory, $"Enable: échec final. {mismatch2}");
                LogRegistrySnapshot("Enable échec");
                return Fail(LocalizationService.T("Les réglages n'ont pas pu être vérifiés après application.", "The settings couldn't be verified after applying them."));
            }

            WindowsAppearanceState.Active = true;
            DiagnosticLog.Write(LogCategory, "Enable réussi. État stocké=Actif");
            LogRegistrySnapshot("Enable succès");
            return Ok();
        }
        catch (Exception ex)
        {
            DiagnosticLog.WriteException(LogCategory, ex, "Enable exception");
            LogRegistrySnapshot("Enable exception");
            return Fail(LocalizationService.T("Impossible d'appliquer les réglages d'apparence.", "Couldn't apply the appearance settings."));
        }
    }

    private static AppearanceTweakResult Disable()
    {
        DiagnosticLog.Write(LogCategory, $"Disable démarré (thread={Environment.CurrentManagedThreadId})");
        LogRegistrySnapshot("Disable avant application");

        try
        {
            ApplyFullAppearance("Disable tentative 1");
            if (!MatchesFullAppearanceProfile(out var mismatch1))
            {
                DiagnosticLog.Write(LogCategory, $"Disable: vérification échouée après tentative 1. {mismatch1}");
                ApplyFullAppearance("Disable tentative 2");
                Thread.Sleep(250);
            }

            if (!MatchesFullAppearanceProfile(out var mismatch2))
            {
                DiagnosticLog.Write(LogCategory, $"Disable: échec final. {mismatch2}");
                LogRegistrySnapshot("Disable échec");
                return Fail(LocalizationService.T("Les réglages n'ont pas pu être vérifiés après restauration.", "The settings couldn't be verified after restoring them."));
            }

            WindowsAppearanceState.Active = false;
            DiagnosticLog.Write(LogCategory, "Disable réussi. État stocké=Inactif");
            LogRegistrySnapshot("Disable succès");
            return Ok();
        }
        catch (Exception ex)
        {
            DiagnosticLog.WriteException(LogCategory, ex, "Disable exception");
            LogRegistrySnapshot("Disable exception");
            return Fail(LocalizationService.T("Impossible de restaurer les réglages d'apparence.", "Couldn't restore the appearance settings."));
        }
    }

    private static void ApplyOptimized(string phase)
    {
        DiagnosticLog.Write(LogCategory, $"ApplyOptimized ({phase})");

        using (var fx = Registry.CurrentUser.CreateSubKey(VisualEffectsKey, true))
            fx?.SetValue("VisualFXSetting", 2, RegistryValueKind.DWord);
        using (var desk = Registry.CurrentUser.CreateSubKey(DesktopKey, true))
            desk?.SetValue("UserPreferencesMask", OptimizedMask, RegistryValueKind.Binary);
        NotifyChanges("ApplyOptimized VFX=2");
        Thread.Sleep(300);

        using (var fx = Registry.CurrentUser.CreateSubKey(VisualEffectsKey, true))
            fx?.SetValue("VisualFXSetting", 3, RegistryValueKind.DWord);

        WriteOptimizedRegistry();

        using (var desk = Registry.CurrentUser.CreateSubKey(DesktopKey, true))
        {
            desk?.SetValue("DragFullWindows", "1", RegistryValueKind.String);
            desk?.SetValue("FontSmoothing", "2", RegistryValueKind.String);
            desk?.SetValue("FontSmoothingType", 2, RegistryValueKind.DWord);
            desk?.SetValue("MinAnimate", "0", RegistryValueKind.String);
            desk?.SetValue("UserPreferencesMask", OptimizedMask, RegistryValueKind.Binary);
        }

        SetSystemParameter(SpiSetCursorShadow, 0);
        SetSystemParameter(SpiSetDragFullWindows, 1);
        SetSystemParameter(SpiSetFontSmoothing, 1);
        SetSystemParameter(SpiSetAnimation, 0);

        NotifyChanges("ApplyOptimized notify");
        WriteOptimizedRegistry();

        using (var desk = Registry.CurrentUser.CreateSubKey(DesktopKey, true))
            desk?.SetValue("UserPreferencesMask", OptimizedMask, RegistryValueKind.Binary);

        NotifyChanges("ApplyOptimized notify final");
        LogRegistrySnapshot($"ApplyOptimized fin ({phase})");
        LogSpiSnapshot($"ApplyOptimized SPI ({phase})");
    }

    private static void ApplyFullAppearance(string phase)
    {
        DiagnosticLog.Write(LogCategory, $"ApplyFullAppearance ({phase})");
        ApplyProfileSnapshot(CreateDefaultRestoredProfile(), phase);
    }

    private static void ApplyProfileSnapshot(AppearanceProfileSnapshot profile, string phase)
    {
        using (var fx = Registry.CurrentUser.CreateSubKey(VisualEffectsKey, true))
            fx?.SetValue("VisualFXSetting", profile.VisualFxSetting, RegistryValueKind.DWord);

        using (var adv = Registry.CurrentUser.CreateSubKey(ExplorerAdvancedKey, true))
        {
            adv?.SetValue("IconsOnly", 0, RegistryValueKind.DWord);
            adv?.SetValue("ListviewAlphaSelect", profile.ListviewAlphaSelect, RegistryValueKind.DWord);
            adv?.SetValue("ListviewShadow", profile.ListviewShadow, RegistryValueKind.DWord);
            adv?.SetValue("TaskbarAnimations", profile.TaskbarAnimations, RegistryValueKind.DWord);
            adv?.SetValue("DisablePreviewDesktop", profile.DisablePreviewDesktop, RegistryValueKind.DWord);
            adv?.SetValue("ListviewSmoothScrolling", profile.ListviewSmoothScrolling, RegistryValueKind.DWord);
            adv?.SetValue("DisableThumbnails", profile.DisableThumbnails, RegistryValueKind.DWord);
        }

        using (var desk = Registry.CurrentUser.CreateSubKey(DesktopKey, true))
        {
            desk?.SetValue("DragFullWindows", profile.DragFullWindows, RegistryValueKind.String);
            desk?.SetValue("FontSmoothing", profile.FontSmoothing, RegistryValueKind.String);
            desk?.SetValue("FontSmoothingType", profile.FontSmoothingType, RegistryValueKind.DWord);
            desk?.SetValue("MinAnimate", profile.MinAnimate, RegistryValueKind.String);
            desk?.SetValue("UserPreferencesMask", NormalizeMask(profile.UserPreferencesMask), RegistryValueKind.Binary);
        }

        var animationOn = string.Equals(profile.MinAnimate, "1", StringComparison.Ordinal) ? 1 : 0;
        SetSystemParameter(SpiSetAnimation, animationOn);
        SetSystemParameter(SpiSetDragFullWindows, string.Equals(profile.DragFullWindows, "1", StringComparison.Ordinal) ? 1 : 0);
        SetSystemParameter(SpiSetFontSmoothing, string.Equals(profile.FontSmoothing, "2", StringComparison.Ordinal) || string.Equals(profile.FontSmoothing, "1", StringComparison.Ordinal) ? 1 : 0);
        SetSystemParameter(SpiSetCursorShadow, 1);

        NotifyChanges($"ApplyProfileSnapshot notify ({phase})");
        LogRegistrySnapshot($"ApplyProfileSnapshot fin ({phase})");
        LogSpiSnapshot($"ApplyProfileSnapshot SPI ({phase})");
    }

    private static void WriteOptimizedRegistry()
    {
        using var adv = Registry.CurrentUser.CreateSubKey(ExplorerAdvancedKey, true);
        adv?.SetValue("IconsOnly", 0, RegistryValueKind.DWord);
        adv?.SetValue("ListviewAlphaSelect", 0, RegistryValueKind.DWord);
        adv?.SetValue("ListviewShadow", 0, RegistryValueKind.DWord);
        adv?.SetValue("TaskbarAnimations", 0, RegistryValueKind.DWord);
        adv?.SetValue("DisablePreviewDesktop", 1, RegistryValueKind.DWord);
        adv?.SetValue("ListviewSmoothScrolling", 0, RegistryValueKind.DWord);
        adv?.SetValue("DisableThumbnails", 0, RegistryValueKind.DWord);
    }

    private static void EnsureRestoredProfileCaptured()
    {
        if (WindowsAppearanceState.RestoredProfile is not null)
            return;

        var captured = CaptureCurrentProfile();
        WindowsAppearanceState.RestoredProfile = captured;
        DiagnosticLog.Write(LogCategory, $"Profil d'origine sauvegardé: mask={BitConverter.ToString(captured.UserPreferencesMask)}");
    }

    private static AppearanceProfileSnapshot CaptureCurrentProfile()
    {
        using var fx = Registry.CurrentUser.OpenSubKey(VisualEffectsKey);
        using var adv = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedKey);
        using var desk = Registry.CurrentUser.OpenSubKey(DesktopKey);
        if (fx is null || adv is null || desk is null)
            return CreateDefaultRestoredProfile();

        return new AppearanceProfileSnapshot
        {
            VisualFxSetting = ReadInt(fx, "VisualFXSetting") is var vfx and >= 0 ? vfx : 1,
            UserPreferencesMask = NormalizeMask(ReadBinary(desk, "UserPreferencesMask")),
            ListviewAlphaSelect = ReadInt(adv, "ListviewAlphaSelect") is var alpha and >= 0 ? alpha : 1,
            ListviewShadow = ReadInt(adv, "ListviewShadow") is var shadow and >= 0 ? shadow : 1,
            TaskbarAnimations = ReadInt(adv, "TaskbarAnimations") is var taskbar and >= 0 ? taskbar : 1,
            DisablePreviewDesktop = ReadInt(adv, "DisablePreviewDesktop") is var peek and >= 0 ? peek : 0,
            ListviewSmoothScrolling = ReadInt(adv, "ListviewSmoothScrolling") is var smooth and >= 0 ? smooth : 1,
            DisableThumbnails = ReadInt(adv, "DisableThumbnails") is var thumbs and >= 0 ? thumbs : 0,
            DragFullWindows = ReadString(desk, "DragFullWindows") is { Length: > 0 } drag ? drag : "1",
            FontSmoothing = ReadString(desk, "FontSmoothing") is { Length: > 0 } font ? font : "2",
            FontSmoothingType = ReadInt(desk, "FontSmoothingType") is var fontType and >= 0 ? fontType : 2,
            MinAnimate = ReadString(desk, "MinAnimate") is { Length: > 0 } minAnim ? minAnim : "1",
        };
    }

    private static AppearanceProfileSnapshot CreateDefaultRestoredProfile() => new();

    private static bool MasksEqual(byte[]? actual, byte[] expected)
    {
        if (actual is null)
            return false;

        for (var i = 0; i < expected.Length; i++)
        {
            if (i >= actual.Length || actual[i] != expected[i])
                return false;
        }

        return true;
    }

    private static byte[] NormalizeMask(byte[]? mask)
    {
        if (mask is null || mask.Length == 0)
            return (byte[])BestAppearanceMask.Clone();

        var normalized = new byte[8];
        Array.Copy(mask, normalized, Math.Min(mask.Length, normalized.Length));
        return normalized;
    }

    private static bool MatchesOptimizedProfile() => MatchesOptimizedProfile(out _);

    private static bool MatchesOptimizedProfile(out string mismatch)
    {
        mismatch = string.Empty;
        try
        {
            using var fx = Registry.CurrentUser.OpenSubKey(VisualEffectsKey);
            using var adv = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedKey);
            using var desk = Registry.CurrentUser.OpenSubKey(DesktopKey);
            if (fx is null || adv is null || desk is null)
            {
                mismatch = "Clés registre introuvables.";
                return false;
            }

            if (ReadInt(fx, "VisualFXSetting") != 3)
            {
                mismatch = $"VisualFXSetting={ReadInt(fx, "VisualFXSetting")} (attendu 3)";
                return false;
            }

            if (!OptimizedAppearanceEffectsActive(adv, desk, out mismatch))
                return false;

            mismatch = "OK";
            return true;
        }
        catch (Exception ex)
        {
            mismatch = $"Exception vérification optimisée: {ex.Message}";
            return false;
        }
    }

    private static bool OptimizedAppearanceEffectsActive(RegistryKey adv, RegistryKey desk)
        => OptimizedAppearanceEffectsActive(adv, desk, out _);

    private static bool OptimizedAppearanceEffectsActive(RegistryKey adv, RegistryKey desk, out string mismatch)
    {
        mismatch = string.Empty;
        var issues = new List<string>();

        var mask = ReadBinary(desk, "UserPreferencesMask");
        if (mask is null || mask.Length < 3)
        {
            mismatch = "UserPreferencesMask absent ou trop court.";
            return false;
        }

        if (!MasksEqual(mask, OptimizedMask))
            issues.Add($"mask={BitConverter.ToString(mask)} (attendu {BitConverter.ToString(OptimizedMask)})");

        if (ReadInt(adv, "ListviewAlphaSelect") != 0)
            issues.Add($"ListviewAlphaSelect={ReadInt(adv, "ListviewAlphaSelect")}");
        if (ReadInt(adv, "ListviewShadow") != 0)
            issues.Add($"ListviewShadow={ReadInt(adv, "ListviewShadow")}");
        if (ReadInt(adv, "TaskbarAnimations") != 0)
            issues.Add($"TaskbarAnimations={ReadInt(adv, "TaskbarAnimations")}");
        if (ReadInt(adv, "DisablePreviewDesktop") != 1)
            issues.Add($"DisablePreviewDesktop={ReadInt(adv, "DisablePreviewDesktop")}");
        if (ReadInt(adv, "ListviewSmoothScrolling") != 0)
            issues.Add($"ListviewSmoothScrolling={ReadInt(adv, "ListviewSmoothScrolling")}");
        if (ReadInt(adv, "DisableThumbnails") != 0)
            issues.Add($"DisableThumbnails={ReadInt(adv, "DisableThumbnails")}");

        if (!string.Equals(ReadString(desk, "DragFullWindows"), "1", StringComparison.Ordinal))
            issues.Add($"DragFullWindows={ReadString(desk, "DragFullWindows")}");
        if (!string.Equals(ReadString(desk, "FontSmoothing"), "2", StringComparison.Ordinal))
            issues.Add($"FontSmoothing={ReadString(desk, "FontSmoothing")}");
        if (ReadInt(desk, "FontSmoothingType") != 2)
            issues.Add($"FontSmoothingType={ReadInt(desk, "FontSmoothingType")}");
        if (!string.Equals(ReadString(desk, "MinAnimate"), "0", StringComparison.Ordinal))
            issues.Add($"MinAnimate={ReadString(desk, "MinAnimate")}");

        if (issues.Count > 0)
        {
            mismatch = string.Join(", ", issues);
            return false;
        }

        mismatch = "OK";
        return true;
    }

    private static bool MatchesFullAppearanceProfile() => MatchesFullAppearanceProfile(out _);

    private static bool MatchesFullAppearanceProfile(out string mismatch)
    {
        mismatch = string.Empty;
        try
        {
            using var fx = Registry.CurrentUser.OpenSubKey(VisualEffectsKey);
            using var adv = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedKey);
            using var desk = Registry.CurrentUser.OpenSubKey(DesktopKey);
            if (fx is null || adv is null || desk is null)
            {
                mismatch = "Clés registre introuvables.";
                return false;
            }

            var expected = CreateDefaultRestoredProfile();
            if (ReadInt(fx, "VisualFXSetting") != expected.VisualFxSetting)
            {
                mismatch = $"VisualFXSetting={ReadInt(fx, "VisualFXSetting")} (attendu {expected.VisualFxSetting})";
                return false;
            }

            if (!MatchesRestoredProfile(adv, desk, expected, out mismatch))
                return false;

            mismatch = "OK";
            return true;
        }
        catch (Exception ex)
        {
            mismatch = $"Exception vérification complète: {ex.Message}";
            return false;
        }
    }

    private static bool AllAppearanceEffectsEnabled(RegistryKey adv, RegistryKey desk)
        => AllAppearanceEffectsEnabled(adv, desk, out _);

    private static bool MatchesRestoredProfile(RegistryKey adv, RegistryKey desk, AppearanceProfileSnapshot expected, out string mismatch)
    {
        mismatch = string.Empty;
        var issues = new List<string>();

        if (ReadInt(adv, "ListviewAlphaSelect") != expected.ListviewAlphaSelect)
            issues.Add($"ListviewAlphaSelect={ReadInt(adv, "ListviewAlphaSelect")}");
        if (ReadInt(adv, "ListviewShadow") != expected.ListviewShadow)
            issues.Add($"ListviewShadow={ReadInt(adv, "ListviewShadow")}");
        if (ReadInt(adv, "TaskbarAnimations") != expected.TaskbarAnimations)
            issues.Add($"TaskbarAnimations={ReadInt(adv, "TaskbarAnimations")}");
        if (ReadInt(adv, "DisablePreviewDesktop") != expected.DisablePreviewDesktop)
            issues.Add($"DisablePreviewDesktop={ReadInt(adv, "DisablePreviewDesktop")}");
        if (ReadInt(adv, "ListviewSmoothScrolling") != expected.ListviewSmoothScrolling)
            issues.Add($"ListviewSmoothScrolling={ReadInt(adv, "ListviewSmoothScrolling")}");
        if (ReadInt(adv, "DisableThumbnails") != expected.DisableThumbnails)
            issues.Add($"DisableThumbnails={ReadInt(adv, "DisableThumbnails")}");

        if (!string.Equals(ReadString(desk, "DragFullWindows"), expected.DragFullWindows, StringComparison.Ordinal))
            issues.Add($"DragFullWindows={ReadString(desk, "DragFullWindows")}");
        if (!string.Equals(ReadString(desk, "FontSmoothing"), expected.FontSmoothing, StringComparison.Ordinal))
            issues.Add($"FontSmoothing={ReadString(desk, "FontSmoothing")}");
        if (ReadInt(desk, "FontSmoothingType") != expected.FontSmoothingType)
            issues.Add($"FontSmoothingType={ReadInt(desk, "FontSmoothingType")}");
        if (!string.Equals(ReadString(desk, "MinAnimate"), expected.MinAnimate, StringComparison.Ordinal))
            issues.Add($"MinAnimate={ReadString(desk, "MinAnimate")}");

        var mask = ReadBinary(desk, "UserPreferencesMask");
        var expectedMask = NormalizeMask(expected.UserPreferencesMask);
        if (mask is null || !mask.Take(8).SequenceEqual(expectedMask))
            issues.Add($"mask={BitConverter.ToString(mask ?? [])}");

        if (issues.Count > 0)
        {
            mismatch = string.Join(", ", issues);
            return false;
        }

        mismatch = "OK";
        return true;
    }

    private static bool AllAppearanceEffectsEnabled(RegistryKey adv, RegistryKey desk, out string mismatch)
    {
        mismatch = string.Empty;
        var issues = new List<string>();

        var mask = ReadBinary(desk, "UserPreferencesMask");
        if (mask is null || mask.Length < 3)
        {
            mismatch = "UserPreferencesMask absent ou trop court.";
            return false;
        }

        ulong value = 0;
        for (var i = 0; i < Math.Min(mask.Length, 8); i++)
            value |= (ulong)mask[i] << (8 * i);

        void RequireBit(ulong bit, string name)
        {
            if (!HasBit(value, bit))
                issues.Add($"bit {name} manquant");
        }

        RequireBit(0x02, "0x02");
        RequireBit(0x04, "0x04");
        if (!string.Equals(ReadString(desk, "MinAnimate"), "1", StringComparison.Ordinal))
            issues.Add($"MinAnimate={ReadString(desk, "MinAnimate")}");
        if (ReadInt(adv, "TaskbarAnimations") != 1)
            issues.Add($"TaskbarAnimations={ReadInt(adv, "TaskbarAnimations")}");
        if (ReadInt(adv, "DisablePreviewDesktop") != 0)
            issues.Add($"DisablePreviewDesktop={ReadInt(adv, "DisablePreviewDesktop")}");
        RequireBit(0x20, "0x20");
        RequireBit(0x40, "0x40");
        RequireBit(0x80, "0x80");
        RequireBit(0x100, "0x100");
        RequireBit(0x200, "0x200");
        RequireBit(0x400, "0x400");
        if (ReadInt(adv, "ListviewAlphaSelect") != 1)
            issues.Add($"ListviewAlphaSelect={ReadInt(adv, "ListviewAlphaSelect")}");
        if (!string.Equals(ReadString(desk, "DragFullWindows"), "1", StringComparison.Ordinal))
            issues.Add($"DragFullWindows={ReadString(desk, "DragFullWindows")}");
        RequireBit(0x2000, "0x2000");
        if (ReadInt(adv, "ListviewSmoothScrolling") != 1)
            issues.Add($"ListviewSmoothScrolling={ReadInt(adv, "ListviewSmoothScrolling")}");
        if (ReadInt(adv, "ListviewShadow") != 1)
            issues.Add($"ListviewShadow={ReadInt(adv, "ListviewShadow")}");
        if (ReadInt(adv, "DisableThumbnails") != 0)
            issues.Add($"DisableThumbnails={ReadInt(adv, "DisableThumbnails")}");
        RequireBit(0x10000, "0x10000");
        if (!string.Equals(ReadString(desk, "FontSmoothing"), "2", StringComparison.Ordinal))
            issues.Add($"FontSmoothing={ReadString(desk, "FontSmoothing")}");
        if (ReadInt(desk, "FontSmoothingType") != 2)
            issues.Add($"FontSmoothingType={ReadInt(desk, "FontSmoothingType")}");

        if (issues.Count > 0)
        {
            mismatch = string.Join(", ", issues);
            return false;
        }

        mismatch = "OK";
        return true;
    }

    private static bool HasBit(ulong value, ulong bit) => (value & bit) == bit;

    private static int ReadInt(RegistryKey key, string name)
    {
        var value = key.GetValue(name);
        return value switch
        {
            int i => i,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => -1,
        };
    }

    private static string ReadString(RegistryKey key, string name)
        => key.GetValue(name)?.ToString() ?? string.Empty;

    private static byte[]? ReadBinary(RegistryKey key, string name)
        => key.GetValue(name) as byte[];

    private static void SetSystemParameter(int action, int value)
    {
        var ok = SystemParametersInfoSet(action, value, IntPtr.Zero, SpiFUpdateIniFile | SpiFSendChange);
        var error = Marshal.GetLastWin32Error();
        if (!ok && error != 0)
            DiagnosticLog.Write(LogCategory, $"SPI échec action=0x{action:X} value={value} err={error}");
    }

    private static void NotifyChanges(string phase)
    {
        int? exitCode = null;
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = "user32.dll,UpdatePerUserSystemParameters",
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            process?.WaitForExit(5000);
            exitCode = process?.ExitCode;
        }
        catch (Exception ex)
        {
            DiagnosticLog.WriteException(LogCategory, ex, $"{phase}: rundll32");
        }

        SendMessageTimeout(new IntPtr(HwndBroadcast), WmSettingChange, IntPtr.Zero, "VisualEffects",
            0, 1000, out _);
        SendMessageTimeout(new IntPtr(HwndBroadcast), WmSettingChange, IntPtr.Zero, "Desktop",
            0, 1000, out _);

        DiagnosticLog.Write(LogCategory, $"{phase}: rundll32 exit={exitCode?.ToString() ?? "null"}");
    }

    private static void LogRegistrySnapshot(string label)
        => DiagnosticLog.Write(LogCategory, $"{label}: {DescribeRegistrySnapshot()}");

    private static void LogSpiSnapshot(string label)
    {
        var builder = new StringBuilder();
        builder.Append("UiEffects=").Append(GetSystemParameter(SpiGetUiEffects));
        builder.Append(" | ClientArea=").Append(GetSystemParameter(SpiGetClientAreaAnimation));
        builder.Append(" | Menu=").Append(GetSystemParameter(SpiGetMenuAnimation));
        builder.Append(" | Combo=").Append(GetSystemParameter(SpiGetComboBoxAnimation));
        builder.Append(" | ListSmooth=").Append(GetSystemParameter(SpiGetListBoxSmoothScrolling));
        builder.Append(" | SelectionFade=").Append(GetSystemParameter(SpiGetSelectionFade));
        builder.Append(" | TooltipAnim=").Append(GetSystemParameter(SpiGetTooltipAnimation));
        builder.Append(" | TooltipFade=").Append(GetSystemParameter(SpiGetTooltipFade));
        builder.Append(" | CursorShadow=").Append(GetSystemParameter(SpiGetCursorShadow));
        builder.Append(" | DragFull=").Append(GetSystemParameter(SpiGetDragFullWindows));
        builder.Append(" | FontSmooth=").Append(GetSystemParameter(SpiGetFontSmoothing));
        builder.Append(" | Animation=").Append(GetSystemParameter(SpiGetAnimation));
        DiagnosticLog.Write(LogCategory, $"{label}: {builder}");
    }

    private static int GetSystemParameter(int action)
    {
        var value = 0;
        SystemParametersInfoGet(action, 0, ref value, 0);
        return value;
    }

    private static string DescribeRegistrySnapshot()
    {
        try
        {
            using var fx = Registry.CurrentUser.OpenSubKey(VisualEffectsKey);
            using var adv = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedKey);
            using var desk = Registry.CurrentUser.OpenSubKey(DesktopKey);
            if (fx is null || adv is null || desk is null)
                return "registre indisponible";

            var mask = ReadBinary(desk, "UserPreferencesMask");
            var maskText = mask is null ? "null" : BitConverter.ToString(mask);
            var builder = new StringBuilder();
            builder.Append($"storedActive={WindowsAppearanceState.Active}");
            builder.Append($" | checked={CountCheckedEffects(adv, desk)}/17");
            builder.Append($" | VFX={ReadInt(fx, "VisualFXSetting")}");
            builder.Append($" | mask={maskText}");
            builder.Append($" | Alpha={ReadInt(adv, "ListviewAlphaSelect")}");
            builder.Append($" | Shadow={ReadInt(adv, "ListviewShadow")}");
            builder.Append($" | Taskbar={ReadInt(adv, "TaskbarAnimations")}");
            builder.Append($" | PeekOff={ReadInt(adv, "DisablePreviewDesktop")}");
            builder.Append($" | ThumbsOff={ReadInt(adv, "DisableThumbnails")}");
            builder.Append($" | Drag={ReadString(desk, "DragFullWindows")}");
            builder.Append($" | Font={ReadString(desk, "FontSmoothing")}");
            builder.Append($" | MinAnimate={ReadString(desk, "MinAnimate")}");
            builder.Append($" | matchesOptimized={MatchesOptimizedProfile()}");
            builder.Append($" | matchesFull={MatchesFullAppearanceProfile()}");
            return builder.ToString();
        }
        catch (Exception ex)
        {
            return $"erreur snapshot: {ex.Message}";
        }
    }

    private static int CountCheckedEffects(RegistryKey adv, RegistryKey desk)
    {
        var mask = ReadBinary(desk, "UserPreferencesMask");
        ulong value = 0;
        if (mask is not null)
        {
            for (var i = 0; i < Math.Min(mask.Length, 8); i++)
                value |= (ulong)mask[i] << (8 * i);
        }

        bool bit(ulong b) => (value & b) == b;
        var items = new[]
        {
            ReadInt(adv, "DisablePreviewDesktop") == 0,
            ReadInt(adv, "DisableThumbnails") == 0,
            bit(0x40),
            string.Equals(ReadString(desk, "DragFullWindows"), "1", StringComparison.Ordinal),
            bit(0x100),
            bit(0x400),
            bit(0x02),
            string.Equals(ReadString(desk, "MinAnimate"), "1", StringComparison.Ordinal),
            bit(0x1000),
            bit(0x2000),
            bit(0x80),
            bit(0x200),
            bit(0x10),
            ReadInt(adv, "ListviewAlphaSelect") == 1,
            string.Equals(ReadString(desk, "FontSmoothing"), "2", StringComparison.Ordinal),
            ReadInt(adv, "TaskbarAnimations") == 1,
            ReadInt(adv, "ListviewShadow") == 1,
        };

        return items.Count(x => x);
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    private static extern bool SystemParametersInfoSet(int uiAction, int uiParam, IntPtr pvParam, int fWinIni);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    private static extern bool SystemParametersInfoGet(int uiAction, int uiParam, ref int pvParam, int fWinIni);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, string lParam,
        int fuFlags, int uTimeout, out IntPtr lpdwResult);

    private static AppearanceTweakResult Ok() => new() { Success = true };

    private static AppearanceTweakResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
