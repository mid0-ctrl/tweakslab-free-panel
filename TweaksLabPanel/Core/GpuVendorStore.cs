using System.IO;
using System.Text.Json;

namespace TweaksLabPanel.Core;

public sealed class GpuVendorStore
{
    public static GpuVendorStore Instance { get; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TweaksLabPanel",
        "gpu-vendor.json");

    public string? SelectedVendor { get; private set; }

    public void Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return;

            var json = File.ReadAllText(_settingsPath);
            var data = JsonSerializer.Deserialize<VendorData>(json);
            if (data is null || string.IsNullOrWhiteSpace(data.SelectedVendor))
                return;

            var vendor = data.SelectedVendor.Trim().ToLowerInvariant();
            if (vendor is GpuVendor.Nvidia or GpuVendor.Amd)
                SelectedVendor = vendor;
        }
        catch
        {
            SelectedVendor = null;
        }
    }

    public void Save(string vendor)
    {
        vendor = vendor.Trim().ToLowerInvariant();
        if (vendor is not (GpuVendor.Nvidia or GpuVendor.Amd))
            return;

        SelectedVendor = vendor;
        Persist();
    }

    public void Clear()
    {
        SelectedVendor = null;
        Persist();
    }

    private void Persist()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var data = new VendorData { SelectedVendor = SelectedVendor ?? string.Empty };
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch
        {
            // Ignorer.
        }
    }

    private sealed class VendorData
    {
        public string SelectedVendor { get; set; } = string.Empty;
    }
}
