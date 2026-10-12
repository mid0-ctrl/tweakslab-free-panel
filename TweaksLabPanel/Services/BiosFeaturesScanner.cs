using System.Management;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Services;

public static class BiosFeaturesScanner
{
    // Codes de type mémoire (identiques dans Win32_PhysicalMemory.MemoryType et SMBIOSMemoryType).
    private const int Ddr3Type = 24;
    private const int Ddr4Type = 26;
    private const int Ddr5Type = 34;

    // Vitesse par défaut (JEDEC) que la carte mère applique quand XMP / EXPO est coupé.
    private const int Ddr3JedecBase = 1600;
    private const int Ddr4JedecBase = 2666;
    private const int Ddr5JedecBase = 4800;

    private static readonly int[] KnownSpeeds =
    [
        1333, 1600, 1866, 2133, 2400, 2666, 2800, 2933, 3000, 3200, 3333, 3466, 3600, 3733, 3800, 3866, 4000, 4133,
        4266, 4400, 4600, 4800, 5200, 5600, 6000, 6200, 6400, 6600, 6800, 7000, 7200, 7600, 8000, 8200, 8400,
    ];

    // Codes vitesse dans les références des grandes marques (G.Skill, Corsair, Kingston/HyperX, Crucial, TeamGroup, Patriot).
    private static readonly Regex[] PartNumberSpeedPatterns =
    [
        new(@"^F[45]-(?<mhz>\d{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"X[45]M\d[A-Z](?<mhz>\d{4})C", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^(?:KF|HX)[345](?<hecto>\d{2})C", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^BL[SM]?\d+G(?<hecto>\d{2})C", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(?<mhz>\d{4})HC\d", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^PV\w*?\d{1,3}G(?<deca>\d{3})C", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    public static BiosFeaturesSnapshot Scan()
    {
        var xmp = ReadXmpState(out var detail);
        return new BiosFeaturesSnapshot(
            Tpm: ReadTpmState(),
            SecureBoot: ReadSecureBootState(),
            Xmp: xmp,
            XmpDetail: detail);
    }

    private static FeatureActivationState ReadTpmState()
    {
        // 1) Classe Win32_Tpm : complète mais réservée aux administrateurs.
        try
        {
            var scope = new ManagementScope(@"root\CIMV2\Security\MicrosoftTpm");
            scope.Connect();

            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM Win32_Tpm"));
            foreach (ManagementObject obj in searcher.Get())
            {
                var enabled = InvokeTpmFlag(obj, "IsEnabled_", "IsEnabled");
                var activated = InvokeTpmFlag(obj, "IsActivated_", "IsActivated");
                return enabled && activated ? FeatureActivationState.Enabled : FeatureActivationState.Disabled;
            }
        }
        catch
        {
            // Accès refusé sans droits administrateur : on passe à la détection du périphérique.
        }

        // 2) Périphérique TPM vu par Windows (lisible par tout utilisateur).
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Status, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE Service = 'TPM'");
            foreach (ManagementObject obj in searcher.Get())
            {
                var status = obj["Status"]?.ToString();
                var errorCode = Convert.ToInt32(obj["ConfigManagerErrorCode"] ?? 0);
                if (errorCode == 0 && string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                    return FeatureActivationState.Enabled;
            }

            // Aucun TPM opérationnel visible : désactivé dans le BIOS (ou absent).
            return FeatureActivationState.Disabled;
        }
        catch
        {
            return FeatureActivationState.Unknown;
        }
    }

    private static FeatureActivationState ReadSecureBootState()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            if (key is null)
                return FeatureActivationState.NotSupported;

            if (key.GetValue("UEFISecureBootEnabled") is int enabled)
                return enabled == 1 ? FeatureActivationState.Enabled : FeatureActivationState.Disabled;
        }
        catch { }

        return FeatureActivationState.Unknown;
    }

    /// <summary>
    /// XMP / EXPO : compare la vitesse configurée à la vitesse nominale du kit
    /// (référence de la barrette, sinon vitesse annoncée par le BIOS).
    /// </summary>
    public static FeatureActivationState ReadXmpState(out string? detail)
    {
        detail = null;

        try
        {
            var configured = 0;
            var reported = 0;
            var rated = 0;
            int? memoryType = null;
            int? smbiosMemoryType = null;

            using var searcher = new ManagementObjectSearcher(
                "SELECT ConfiguredClockSpeed, Speed, MemoryType, SMBIOSMemoryType, PartNumber FROM Win32_PhysicalMemory");
            foreach (ManagementObject obj in searcher.Get())
            {
                configured = Math.Max(configured, ReadWmiInt(obj["ConfiguredClockSpeed"]));
                reported = Math.Max(reported, ReadWmiInt(obj["Speed"]));
                rated = Math.Max(rated, ParsePartNumberSpeed(obj["PartNumber"]?.ToString()));

                var type = ReadWmiInt(obj["MemoryType"]);
                if (type > 0)
                    memoryType = type;

                var smbiosType = ReadWmiInt(obj["SMBIOSMemoryType"]);
                if (smbiosType > 0)
                    smbiosMemoryType = smbiosType;
            }

            if (configured <= 0)
                configured = reported;

            if (configured <= 0)
                return FeatureActivationState.Unknown;

            var nominal = Math.Max(rated, reported);
            var generation = ResolveDdrGeneration(memoryType, smbiosMemoryType);

            // Certains BIOS donnent l'horloge réelle (moitié de la vitesse DDR) : 1600 pour de la DDR4-3200.
            // Une vitesse DDR4 / DDR5 sous 1600 MHz n'existe pas : dans ce cas seulement, on double.
            if (generation >= 4 && configured < 1600)
                configured *= 2;

            detail = $"{configured} MHz";

            // Portable : en général aucun profil XMP / EXPO à activer, la RAM tourne à sa vitesse d'origine.
            if (IsLaptop())
                return FeatureActivationState.NotSupported;

            var jedecBase = generation switch
            {
                3 => Ddr3JedecBase,
                5 => Ddr5JedecBase,
                _ => Ddr4JedecBase,
            };

            // Au-dessus de la vitesse par défaut → profil chargé (même un peu sous la vitesse du kit, ex. 4 barrettes).
            if (configured > jedecBase + 100)
                return FeatureActivationState.Enabled;

            // Référence du kit connue et RAM à sa vitesse prévue (kits lents type DDR4-2400 / 2666).
            if (rated > 0 && configured >= rated * 0.95)
                return FeatureActivationState.Enabled;

            // Kit prévu pour plus vite que la vitesse appliquée → profil non chargé.
            if (nominal > configured * 1.05 && nominal > jedecBase)
                return FeatureActivationState.Disabled;

            // Vitesse par défaut sur une carte mère gaming : la RAM gaming tourne au-dessus une fois XMP / EXPO activé.
            // Sur un PC de marque (BIOS souvent verrouillé), on ne conclut pas.
            return HasEnthusiastMotherboard()
                ? FeatureActivationState.Disabled
                : FeatureActivationState.Unknown;
        }
        catch
        {
            return FeatureActivationState.Unknown;
        }
    }

    private static int ParsePartNumberSpeed(string? partNumber)
    {
        if (string.IsNullOrWhiteSpace(partNumber))
            return 0;

        var part = partNumber.Trim();
        foreach (var pattern in PartNumberSpeedPatterns)
        {
            var match = pattern.Match(part);
            if (!match.Success)
                continue;

            var speed = match.Groups["mhz"].Success ? int.Parse(match.Groups["mhz"].Value)
                : match.Groups["hecto"].Success ? int.Parse(match.Groups["hecto"].Value) * 100
                : match.Groups["deca"].Success ? int.Parse(match.Groups["deca"].Value) * 10
                : 0;

            var known = SnapToKnownSpeed(speed);
            if (known > 0)
                return known;
        }

        return 0;
    }

    private static int SnapToKnownSpeed(int speed)
    {
        if (speed <= 0)
            return 0;

        foreach (var known in KnownSpeeds)
        {
            if (Math.Abs(known - speed) <= 40)
                return known;
        }

        return 0;
    }

    private static bool IsLaptop()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["ChassisTypes"] is not ushort[] types)
                    continue;

                // Portable, notebook, sub-notebook, convertible, détachable, tablette…
                if (types.Any(t => t is 8 or 9 or 10 or 11 or 12 or 14 or 18 or 21 or 30 or 31 or 32))
                    return true;
            }
        }
        catch { }

        return false;
    }

    private static readonly string[] EnthusiastBoardVendors =
    [
        "ASUS", "ASRock", "Gigabyte", "Micro-Star", "MSI", "Biostar", "EVGA", "NZXT", "Colorful", "Maxsun",
    ];

    private static bool HasEnthusiastMotherboard()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_BaseBoard");
            foreach (ManagementObject obj in searcher.Get())
            {
                var manufacturer = obj["Manufacturer"]?.ToString() ?? string.Empty;
                if (EnthusiastBoardVendors.Any(v => manufacturer.Contains(v, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }
        catch { }

        return false;
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
        if (memoryType == Ddr5Type || smbiosMemoryType == Ddr5Type)
            return 5;

        if (memoryType == Ddr4Type || smbiosMemoryType == Ddr4Type)
            return 4;

        if (memoryType == Ddr3Type || smbiosMemoryType == Ddr3Type)
            return 3;

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
