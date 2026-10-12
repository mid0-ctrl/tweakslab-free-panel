using System.Management;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Services;

public sealed record DetectedGpu(string Vendor, string Name);

/// <summary>Détecte la carte graphique principale (NVIDIA / AMD prioritaires sur Intel) via son identifiant matériel.</summary>
public static class GpuDetector
{
    private static readonly object Gate = new();
    private static Task<DetectedGpu?>? _detection;

    public static Task<DetectedGpu?> DetectAsync()
    {
        lock (Gate)
            return _detection ??= Task.Run(Detect);
    }

    private static DetectedGpu? Detect()
    {
        try
        {
            // Priorité : NVIDIA, puis carte AMD dédiée (Radeon RX / Pro), puis puce AMD intégrée
            // (ex. Ryzen 7000 + RTX : on veut la RTX), puis Intel.
            var candidates = new List<(int Priority, DetectedGpu Gpu)>();
            using var searcher = new ManagementObjectSearcher("SELECT Name, PNPDeviceID FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim() ?? string.Empty;
                var pnp = obj["PNPDeviceID"]?.ToString() ?? string.Empty;

                if (pnp.Contains("VEN_10DE", StringComparison.OrdinalIgnoreCase))
                    candidates.Add((0, new DetectedGpu(GpuVendor.Nvidia, name)));
                else if (pnp.Contains("VEN_1002", StringComparison.OrdinalIgnoreCase))
                    candidates.Add((IsDedicatedAmd(name) ? 1 : 2, new DetectedGpu(GpuVendor.Amd, name)));
                else if (pnp.Contains("VEN_8086", StringComparison.OrdinalIgnoreCase))
                    candidates.Add((3, new DetectedGpu(GpuVendor.Intel, name)));
            }

            return candidates.Count == 0
                ? null
                : candidates.OrderBy(c => c.Priority).First().Gpu;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsDedicatedAmd(string name) =>
        name.Contains(" RX ", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Radeon RX", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Radeon Pro", StringComparison.OrdinalIgnoreCase)
        || name.Contains("FirePro", StringComparison.OrdinalIgnoreCase);
}
