using System.Management;
using Microsoft.Win32;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Services;

public static class BiosFeaturesScanner
{
    private const int Ddr4MemoryType = 24;
    private const int Ddr5MemoryType = 34;

    public static BiosFeaturesSnapshot Scan() =>
        new(
            Tpm: ReadTpmState(),
            SecureBoot: ReadSecureBootState(),
            Xmp: ReadXmpState(out var detail),
            XmpDetail: detail);

    private static FeatureActivationState ReadTpmState()
    {
        try
        {
            var scope = new ManagementScope(@"root\CIMV2\Security\MicrosoftTpm");
            scope.Connect();

            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM Win32_Tpm"));
            foreach (ManagementObject obj in searcher.Get())
            {
                var enabled = InvokeTpmFlag(obj, "IsEnabled_", "IsEnabled");
                var activated = InvokeTpmFlag(obj, "IsActivated_", "IsActivated");

                if (enabled && activated)
                    return FeatureActivationState.Enabled;

                if (!enabled || !activated)
                    return FeatureActivationState.Disabled;
            }
        }
        catch (ManagementException)
        {
            return FeatureActivationState.NotSupported;
        }
        catch
        {
            // WMI indisponible : repli registre Windows 11 (TPM requis)
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\TPM\WMI");
                if (key?.GetValue("WindowsAIKHash") is not null || key?.GetValue("OSInfo") is not null)
                    return FeatureActivationState.Enabled;
            }
            catch { }
        }

        return FeatureActivationState.Unknown;
    }

    private static FeatureActivationState ReadSecureBootState()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            if (key?.GetValue("UEFISecureBootEnabled") is int enabled)
                return enabled == 1 ? FeatureActivationState.Enabled : FeatureActivationState.Disabled;
        }
        catch { }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            if (key is null)
                return FeatureActivationState.NotSupported;
        }
        catch { }

        return FeatureActivationState.Unknown;
    }

    private static FeatureActivationState ReadXmpState(out string? detail)
    {
        detail = null;

        try
        {
            int? configured = null;
            int? rated = null;
            int? memoryType = null;
            int? smbiosMemoryType = null;

            using var searcher = new ManagementObjectSearcher(
                "SELECT ConfiguredClockSpeed, Speed, MemoryType, SMBIOSMemoryType FROM Win32_PhysicalMemory");
            foreach (ManagementObject obj in searcher.Get())
            {
                var configuredSpeed = ReadWmiInt(obj["ConfiguredClockSpeed"]);
                if (configuredSpeed > 0)
                    configured = Math.Max(configured ?? 0, configuredSpeed);

                var moduleSpeed = ReadWmiInt(obj["Speed"]);
                if (moduleSpeed > 0)
                    rated = Math.Max(rated ?? 0, moduleSpeed);

                var type = ReadWmiInt(obj["MemoryType"]);
                if (type > 0)
                    memoryType = type;

                var smbiosType = ReadWmiInt(obj["SMBIOSMemoryType"]);
                if (smbiosType > 0)
                    smbiosMemoryType = smbiosType;
            }

            if (configured is null or 0)
                return FeatureActivationState.Unknown;

            detail = $"{configured} MHz";
            var ddrGeneration = ResolveDdrGeneration(memoryType, smbiosMemoryType);
            var jedecMax = ddrGeneration == 5 ? 4800 : 2666;
            var profileSpeed = rated ?? configured.Value;

            if (configured > jedecMax + 133)
                return FeatureActivationState.Enabled;

            if (profileSpeed > jedecMax && configured <= jedecMax)
                return FeatureActivationState.Disabled;

            if (profileSpeed > jedecMax && configured >= profileSpeed * 0.92)
                return FeatureActivationState.Enabled;

            if (ddrGeneration == 4 && configured <= 2666 && profileSpeed > 2666)
                return FeatureActivationState.Disabled;

            if (ddrGeneration == 5 && configured <= 4800 && profileSpeed > 5200)
                return FeatureActivationState.Disabled;

            // Même vitesse configurée et nominale au-dessus du JEDEC → XMP/EXPO actif
            if (configured >= 3000)
                return FeatureActivationState.Enabled;
        }
        catch { }

        return FeatureActivationState.Unknown;
    }

    private static int ReadWmiInt(object? value) => value switch
    {
        ushort u => u,
        uint ui => (int)ui,
        int i => i,
        short s => s,
        byte b => b,
        long l => (int)l,
        _ => 0,
    };

    private static int ResolveDdrGeneration(int? memoryType, int? smbiosMemoryType)
    {
        if (memoryType == Ddr5MemoryType || smbiosMemoryType == 34)
            return 5;

        if (memoryType == Ddr4MemoryType || smbiosMemoryType == 26)
            return 4;

        return 4;
    }

    private static bool InvokeTpmFlag(ManagementObject obj, string methodName, string outputName)
    {
        try
        {
            var output = obj.InvokeMethod(methodName, null);
            if (output is not ManagementBaseObject result)
                return false;

            if (result["ReturnValue"] is uint code && code != 0)
                return false;

            return result[outputName] is bool value && value;
        }
        catch
        {
            return false;
        }
    }
}
