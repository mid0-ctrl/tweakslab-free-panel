using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Services;

public sealed class DisplayModeInfo
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int RefreshHz { get; init; }

    public string ResolutionLabel => $"{Width} x {Height}";
    public string RefreshLabel => $"{RefreshHz} Hz";
    public override string ToString() => $"{ResolutionLabel} @ {RefreshHz} Hz";

    public override bool Equals(object? obj) =>
        obj is DisplayModeInfo other &&
        Width == other.Width && Height == other.Height && RefreshHz == other.RefreshHz;

    public override int GetHashCode() => HashCode.Combine(Width, Height, RefreshHz);
}

public static class DisplaySettingsService
{
    private const int EnumCurrentSettings = -1;
    private const int DmPelsWidth = 0x80000;
    private const int DmPelsHeight = 0x100000;
    private const int DmDisplayFrequency = 0x400000;
    private const int DmFields = 0x80000 | 0x100000 | 0x400000;
    private const int DispChangeSuccessful = 0;
    private const int DispChangeRestart = 1;

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern int EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern int ChangeDisplaySettings(ref DevMode devMode, int flags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    public static DisplayModeInfo? GetCurrentMode()
    {
        var devMode = CreateDevMode();
        if (EnumDisplaySettings(null, EnumCurrentSettings, ref devMode) == 0)
            return null;

        return new DisplayModeInfo
        {
            Width = devMode.dmPelsWidth,
            Height = devMode.dmPelsHeight,
            RefreshHz = devMode.dmDisplayFrequency,
        };
    }

    public static IReadOnlyList<DisplayModeInfo> GetSupportedModes()
    {
        var modes = new HashSet<DisplayModeInfo>();
        var devMode = CreateDevMode();

        for (var i = 0; EnumDisplaySettings(null, i, ref devMode) != 0; i++)
        {
            if (devMode.dmPelsWidth < 800 || devMode.dmPelsHeight < 600 || devMode.dmDisplayFrequency < 30)
                continue;

            modes.Add(new DisplayModeInfo
            {
                Width = devMode.dmPelsWidth,
                Height = devMode.dmPelsHeight,
                RefreshHz = devMode.dmDisplayFrequency,
            });
        }

        return modes
            .OrderByDescending(m => m.Width * m.Height)
            .ThenByDescending(m => m.RefreshHz)
            .ToList();
    }

    public static bool TryApply(DisplayModeInfo mode, out string message)
    {
        var devMode = CreateDevMode();
        devMode.dmFields = DmFields;
        devMode.dmPelsWidth = mode.Width;
        devMode.dmPelsHeight = mode.Height;
        devMode.dmDisplayFrequency = mode.RefreshHz;

        var result = ChangeDisplaySettings(ref devMode, 0);
        message = result switch
        {
            DispChangeSuccessful => LocalizationService.T(
                "Paramètres d'affichage appliqués.",
                "Display settings applied."),
            DispChangeRestart => LocalizationService.T(
                "Paramètres appliqués. Un redémarrage peut être nécessaire.",
                "Settings applied. A restart may be required."),
            _ => LocalizationService.T(
                $"Impossible d'appliquer le mode ({result}).",
                $"Couldn't apply the display mode ({result})."),
        };
        return result is DispChangeSuccessful or DispChangeRestart;
    }

    private static DevMode CreateDevMode() => new()
    {
        dmDeviceName = new string('\0', 32),
        dmFormName = new string('\0', 32),
        dmSize = (short)Marshal.SizeOf<DevMode>(),
    };
}
