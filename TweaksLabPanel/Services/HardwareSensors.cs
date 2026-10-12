using System.Management;
using System.Runtime.InteropServices;

namespace TweaksLabPanel.Services;

/// <summary>
/// Lectures capteurs réelles, sans pilote ni droits administrateur.
/// Aucune valeur par défaut : null = capteur non accessible sur ce PC.
/// </summary>
public static class HardwareSensors
{
    private static readonly TimeSpan UnavailableRetryDelay = TimeSpan.FromMinutes(2);

    private static readonly string[] MonitorNamespaces =
    [
        @"root\LibreHardwareMonitor",
        @"root\OpenHardwareMonitor",
    ];

    private static readonly object CpuLock = new();
    private static (long Idle, long Total)? _lastCpuSample;

    private static readonly TimeSpan AcpiCacheDuration = TimeSpan.FromSeconds(10);

    private static DateTime _monitorUnavailableUntil;
    private static DateTime _acpiUnavailableUntil;
    private static (DateTime ReadAtUtc, double? Value) _acpiCache;

    /// <summary>
    /// true quand la dernière température CPU vient de la zone thermique ACPI (capteur de la carte mère,
    /// proche du processeur mais pas exact). false quand elle vient de LibreHardwareMonitor.
    /// </summary>
    public static bool CpuTemperatureIsApproximate { get; private set; }

    /// <summary>Température GPU : pilote graphique Windows (comme le Gestionnaire des tâches), sinon LibreHardwareMonitor.</summary>
    public static double? ReadGpuTemperatureC()
        => ReadGpuTemperatureFromWddm() ?? ReadMonitorAppTemperature(gpu: true);

    /// <summary>Température CPU : LibreHardwareMonitor si lancé, sinon zone thermique ACPI exposée par la carte mère.</summary>
    public static double? ReadCpuTemperatureC()
    {
        if (ReadMonitorAppTemperature(gpu: false) is { } exact)
        {
            CpuTemperatureIsApproximate = false;
            return exact;
        }

        var approximate = ReadAcpiThermalZone();
        CpuTemperatureIsApproximate = approximate is not null;
        return approximate;
    }

    /// <summary>Charge CPU globale (%) entre deux mesures.</summary>
    public static double? SampleCpuUsagePercent()
    {
        if (!TryReadCpuTimes(out var sample))
            return null;

        (long Idle, long Total) previous;
        lock (CpuLock)
        {
            if (_lastCpuSample is null)
            {
                _lastCpuSample = sample;
                previous = default;
            }
            else
            {
                previous = _lastCpuSample.Value;
                _lastCpuSample = sample;
            }
        }

        if (previous == default)
        {
            Thread.Sleep(250);
            if (!TryReadCpuTimes(out var second))
                return null;

            lock (CpuLock)
                _lastCpuSample = second;

            previous = sample;
            sample = second;
        }

        var totalDelta = sample.Total - previous.Total;
        var idleDelta = sample.Idle - previous.Idle;
        if (totalDelta <= 0)
            return null;

        var usage = (1.0 - (double)idleDelta / totalDelta) * 100.0;
        return Math.Round(Math.Clamp(usage, 0, 100), 0);
    }

    /// <summary>RAM utilisée / installée en Go.</summary>
    public static (double UsedGb, double TotalGb) ReadRam()
    {
        try
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (!GlobalMemoryStatusEx(ref status))
                return (0, 0);

            var totalGb = GetPhysicallyInstalledSystemMemory(out var installedKb) && installedKb > 0
                ? installedKb / 1024.0 / 1024.0
                : status.TotalPhys / 1024.0 / 1024.0 / 1024.0;
            var usedGb = (status.TotalPhys - status.AvailPhys) / 1024.0 / 1024.0 / 1024.0;
            return (Math.Round(Math.Max(0, usedGb), 1), Math.Round(totalGb, 1));
        }
        catch
        {
            return (0, 0);
        }
    }

    private static double? ReadGpuTemperatureFromWddm()
    {
        try
        {
            var enumArgs = new D3dkmtEnumAdapters2();
            var count = D3DKMTEnumAdapters2(ref enumArgs) == 0 && enumArgs.NumAdapters > 0
                ? enumArgs.NumAdapters
                : 16u;

            var entrySize = Marshal.SizeOf<D3dkmtAdapterInfo>();
            var buffer = Marshal.AllocHGlobal(entrySize * (int)count);
            try
            {
                enumArgs.NumAdapters = count;
                enumArgs.Adapters = buffer;
                if (D3DKMTEnumAdapters2(ref enumArgs) != 0)
                    return null;

                double? hottest = null;
                for (var i = 0; i < enumArgs.NumAdapters; i++)
                {
                    var info = Marshal.PtrToStructure<D3dkmtAdapterInfo>(buffer + i * entrySize);
                    try
                    {
                        var temp = QueryAdapterTemperature(info.Adapter);
                        if (temp is > 0 and < 130 && (hottest is null || temp > hottest))
                            hottest = temp;
                    }
                    finally
                    {
                        var close = new D3dkmtCloseAdapter { Adapter = info.Adapter };
                        D3DKMTCloseAdapter(ref close);
                    }
                }

                return hottest is null ? null : Math.Round(hottest.Value, 0);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return null;
        }
    }

    private static double? QueryAdapterTemperature(uint adapter)
    {
        var size = Marshal.SizeOf<D3dkmtAdapterPerfData>();
        var data = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(new D3dkmtAdapterPerfData(), data, false);
            var query = new D3dkmtQueryAdapterInfo
            {
                Adapter = adapter,
                Type = KmtQaiAdapterPerfData,
                PrivateDriverData = data,
                PrivateDriverDataSize = (uint)size,
            };

            if (D3DKMTQueryAdapterInfo(ref query) != 0)
                return null;

            var perf = Marshal.PtrToStructure<D3dkmtAdapterPerfData>(data);
            return perf.Temperature > 0 ? perf.Temperature / 10.0 : null;
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }

    private static double? ReadMonitorAppTemperature(bool gpu)
    {
        if (DateTime.UtcNow < _monitorUnavailableUntil)
            return null;

        var anyNamespace = false;
        foreach (var ns in MonitorNamespaces)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    ns,
                    "SELECT Name, Identifier, Value FROM Sensor WHERE SensorType='Temperature'");

                double? hottest = null;
                foreach (ManagementObject obj in searcher.Get())
                {
                    anyNamespace = true;
                    var id = obj["Identifier"]?.ToString() ?? string.Empty;
                    var name = obj["Name"]?.ToString() ?? string.Empty;
                    var value = Convert.ToDouble(obj["Value"] ?? 0);

                    var matches = gpu
                        ? id.Contains("/gpu", StringComparison.OrdinalIgnoreCase)
                          && name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                        : (id.Contains("/amdcpu", StringComparison.OrdinalIgnoreCase)
                           || id.Contains("/intelcpu", StringComparison.OrdinalIgnoreCase))
                          && (name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                              || name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                              || name.Contains("Tdie", StringComparison.OrdinalIgnoreCase));

                    if (matches && value is > 0 and < 130)
                        hottest = Math.Max(hottest ?? 0, value);
                }

                if (hottest is not null)
                    return Math.Round(hottest.Value, 0);
            }
            catch
            {
                // Logiciel de monitoring absent.
            }
        }

        if (!anyNamespace)
            _monitorUnavailableUntil = DateTime.UtcNow + UnavailableRetryDelay;

        return null;
    }

    private static double? ReadAcpiThermalZone()
    {
        if (DateTime.UtcNow < _acpiUnavailableUntil)
            return null;

        // Requête WMI coûteuse : une lecture toutes les 10 s suffit.
        if (DateTime.UtcNow - _acpiCache.ReadAtUtc < AcpiCacheDuration)
            return _acpiCache.Value;

        double? hottest = null;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Temperature, HighPrecisionTemperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");
            foreach (ManagementObject obj in searcher.Get())
            {
                var tenthsKelvin = Convert.ToDouble(obj["HighPrecisionTemperature"] ?? 0);
                var kelvin = tenthsKelvin > 0 ? tenthsKelvin / 10.0 : Convert.ToDouble(obj["Temperature"] ?? 0);
                var celsius = kelvin - 273.15;
                if (IsPlausibleZone(celsius))
                    hottest = Math.Max(hottest ?? celsius, celsius);
            }
        }
        catch
        {
            // Compteur indisponible.
        }

        if (hottest is null)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\WMI",
                    "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var celsius = Convert.ToDouble(obj["CurrentTemperature"] ?? 0) / 10.0 - 273.15;
                    if (IsPlausibleZone(celsius))
                        hottest = Math.Max(hottest ?? celsius, celsius);
                }
            }
            catch
            {
                // Classe réservée aux administrateurs ou absente.
            }
        }

        if (hottest is null)
        {
            _acpiUnavailableUntil = DateTime.UtcNow + UnavailableRetryDelay;
            return null;
        }

        var rounded = Math.Round(hottest.Value, 0);
        _acpiCache = (DateTime.UtcNow, rounded);
        return rounded;
    }

    // Beaucoup de cartes mères renvoient une zone figée à 27,8 °C : on l'écarte.
    private static bool IsPlausibleZone(double celsius) => celsius is >= 28 and < 110;

    private static bool TryReadCpuTimes(out (long Idle, long Total) sample)
    {
        sample = default;
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return false;

        sample = (idle, kernel + user);
        return true;
    }

    private const int KmtQaiAdapterPerfData = 62;

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3dkmtAdapterInfo
    {
        public uint Adapter;
        public Luid AdapterLuid;
        public uint NumOfSources;
        public int PrecisePresentRegionsPreferred;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3dkmtEnumAdapters2
    {
        public uint NumAdapters;
        public IntPtr Adapters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3dkmtQueryAdapterInfo
    {
        public uint Adapter;
        public int Type;
        public IntPtr PrivateDriverData;
        public uint PrivateDriverDataSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3dkmtAdapterPerfData
    {
        public uint PhysicalAdapterIndex;
        public ulong MemoryFrequency;
        public ulong MaxMemoryFrequency;
        public ulong MaxMemoryFrequencyOc;
        public ulong MemoryBandwidth;
        public ulong PcieBandwidth;
        public uint FanRpm;
        public uint Power;
        public uint Temperature;
        public byte PowerStateOverride;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3dkmtCloseAdapter
    {
        public uint Adapter;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTEnumAdapters2(ref D3dkmtEnumAdapters2 args);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTQueryAdapterInfo(ref D3dkmtQueryAdapterInfo args);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTCloseAdapter(ref D3dkmtCloseAdapter args);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out long totalKb);
}
