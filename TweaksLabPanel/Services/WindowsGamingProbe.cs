using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Services;

/// <summary>
/// Lecture (sans droits administrateur) des réglages Windows qui comptent pour le jeu.
/// null = réglage impossible à lire sur ce PC (le critère n'est alors pas compté dans le score).
/// </summary>
public static class WindowsGamingProbe
{
    private static readonly Guid HighPerformancePlan = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    private static readonly Guid UltimatePerformancePlan = new("e9a42b02-d5df-448d-aa00-03f14749eb61");

    public static double UptimeDays => Environment.TickCount64 / 1000.0 / 3600.0 / 24.0;

    /// <summary>Plan d'alimentation actif orienté performances (TweaksLab, Performances élevées ou Performances optimales).</summary>
    public static bool? IsPerformancePowerPlanActive(out string? planName)
    {
        planName = null;
        try
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out var guidPtr) != 0)
                return null;

            Guid active;
            try
            {
                active = Marshal.PtrToStructure<Guid>(guidPtr);
            }
            finally
            {
                LocalFree(guidPtr);
            }

            var tweaksLabPlan = TweaksLabPowerPlanState.PlanGuid;
            var isTweaksLab = Guid.TryParse(tweaksLabPlan, out var tweaksLabGuid) && tweaksLabGuid == active;

            planName = isTweaksLab ? "TweaksLab Community"
                : active == HighPerformancePlan ? "Performances élevées"
                : active == UltimatePerformancePlan ? "Performances optimales"
                : null;

            return isTweaksLab || active == HighPerformancePlan || active == UltimatePerformancePlan;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Mode Jeu Windows (activé par défaut si la clé est absente).</summary>
    public static bool? IsGameModeEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
            return key?.GetValue("AutoGameModeEnabled") is not int value || value != 0;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Captures Xbox (Game DVR) coupées : rien n'enregistre en arrière-plan pendant les parties.</summary>
    public static bool? IsBackgroundCaptureOff()
    {
        try
        {
            using var gameConfig = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore");
            using var gameDvr = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR");

            if (gameDvr?.GetValue("HistoricalCaptureEnabled") is int historical && historical == 1)
                return false;

            var dvrEnabled = gameConfig?.GetValue("GameDVR_Enabled") as int?;
            var appCapture = gameDvr?.GetValue("AppCaptureEnabled") as int?;

            if (dvrEnabled == 0 || appCapture == 0)
                return true;

            if (dvrEnabled == 1 || appCapture == 1)
                return false;

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Planification GPU accélérée par le matériel (null si la carte / le pilote ne la proposent pas).</summary>
    public static bool? IsHardwareGpuSchedulingEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
            return key?.GetValue("HwSchMode") is int mode ? mode == 2 : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Effets visuels réglés pour les performances (profil TweaksLab ou « Ajuster afin d'obtenir les meilleures performances »).</summary>
    public static bool? AreVisualEffectsOptimized()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
            if (key?.GetValue("VisualFXSetting") is int setting && setting == 2)
                return true;

            return WindowsAppearanceService.IsActive();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>« Améliorer la précision du pointeur » (accélération souris) désactivé.</summary>
    public static bool? IsMouseAccelerationOff()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse");
            return key?.GetValue("MouseSpeed")?.ToString() switch
            {
                "0" => true,
                "1" or "2" => false,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Fréquence actuelle de l'écran principal et fréquence maximale disponible à la même résolution.</summary>
    public static (int Current, int Max)? ReadRefreshRates()
    {
        try
        {
            var current = DisplaySettingsService.GetCurrentMode();
            if (current is null || current.RefreshHz <= 1)
                return null;

            var max = DisplaySettingsService.GetSupportedModes()
                .Where(m => m.Width == current.Width && m.Height == current.Height)
                .Select(m => m.RefreshHz)
                .DefaultIfEmpty(current.RefreshHz)
                .Max();

            return (current.RefreshHz, Math.Max(max, current.RefreshHz));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Windows installé sur un SSD (disque sans temps d'accès mécanique).</summary>
    public static bool? IsSystemDriveSsd()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrEmpty(root))
                return null;

            var volumePath = @"\\.\" + root.TrimEnd('\\');
            using var handle = CreateFile(volumePath, 0, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle.IsInvalid)
                return null;

            var query = new StoragePropertyQuery { PropertyId = StorageDeviceSeekPenaltyProperty, QueryType = PropertyStandardQuery };
            var ok = DeviceIoControl(
                handle,
                IoctlStorageQueryProperty,
                ref query,
                Marshal.SizeOf<StoragePropertyQuery>(),
                out DeviceSeekPenaltyDescriptor descriptor,
                Marshal.SizeOf<DeviceSeekPenaltyDescriptor>(),
                out _,
                IntPtr.Zero);

            return ok ? !descriptor.IncursSeekPenalty : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Âge (en jours) du pilote de la carte graphique principale.</summary>
    public static int? ReadGpuDriverAgeDays()
    {
        try
        {
            var candidates = new List<(int Priority, DateTime Date)>();
            using var searcher = new ManagementObjectSearcher("SELECT Name, PNPDeviceID, DriverDate FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var raw = obj["DriverDate"]?.ToString();
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var name = obj["Name"]?.ToString() ?? string.Empty;
                var pnp = obj["PNPDeviceID"]?.ToString() ?? string.Empty;
                var priority = pnp.Contains("VEN_10DE", StringComparison.OrdinalIgnoreCase) ? 0
                    : pnp.Contains("VEN_1002", StringComparison.OrdinalIgnoreCase)
                        ? (name.Contains("RX", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                    : pnp.Contains("VEN_8086", StringComparison.OrdinalIgnoreCase) ? 3
                    : 4;

                candidates.Add((priority, ManagementDateTimeConverter.ToDateTime(raw)));
            }

            if (candidates.Count == 0)
                return null;

            var driverDate = candidates.OrderBy(c => c.Priority).First().Date;
            return Math.Max(0, (int)(DateTime.Now - driverDate).TotalDays);
        }
        catch
        {
            return null;
        }
    }

    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;
    private const uint IoctlStorageQueryProperty = 0x2D1400;
    private const int StorageDeviceSeekPenaltyProperty = 7;
    private const int PropertyStandardQuery = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct StoragePropertyQuery
    {
        public int PropertyId;
        public int QueryType;
        public byte AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceSeekPenaltyDescriptor
    {
        public uint Version;
        public uint Size;
        [MarshalAs(UnmanagedType.U1)]
        public bool IncursSeekPenalty;
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device, uint ioControlCode,
        ref StoragePropertyQuery inBuffer, int inBufferSize,
        out DeviceSeekPenaltyDescriptor outBuffer, int outBufferSize,
        out uint bytesReturned, IntPtr overlapped);
}
