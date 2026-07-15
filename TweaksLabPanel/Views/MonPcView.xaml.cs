using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;
using TweaksLabPanel.Services;

namespace TweaksLabPanel.Views;

public partial class MonPcView : UserControl
{
    private SystemSpecsSnapshot? _snapshot;

    public MonPcView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshSpecs();
    }

    public void RefreshSpecs()
    {
        _snapshot = SystemSpecsScanner.Scan();
        CpuTempText.Text = $"{_snapshot.CpuTempC:0}°C";
        GpuTempText.Text = $"{_snapshot.GpuTempC:0}°C";
        OsText.Text = _snapshot.OperatingSystem;
        CpuText.Text = $"{_snapshot.Processor} ({_snapshot.ProcessorDetails})";
        GpuText.Text = _snapshot.GraphicsCard;
        RamText.Text = _snapshot.Memory;
        BoardText.Text = _snapshot.Motherboard;
        StorageText.Text = _snapshot.Storage;

        var freePercent = _snapshot.SystemDriveFreePercent;
        DiskBar.Value = freePercent;
        DiskPercentText.Text = $"{freePercent:0.#}%";
        DiskSpaceText.Text = FormatBytes(_snapshot.SystemDriveFreeBytes) + " / " + FormatBytes(_snapshot.SystemDriveTotalBytes);
        DiskWarningText.Visibility = freePercent < 10 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CopySpecs_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null)
            RefreshSpecs();

        if (_snapshot is null)
            return;

        try { Clipboard.SetText(SystemSpecsScanner.FormatSpecsText(_snapshot).Trim()); } catch { }
    }

    private void ShareSpecs_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null)
            RefreshSpecs();

        if (_snapshot is null)
            return;

        var name = UserProfileStore.Instance.ResolvedDisplayName;

        if (!ShareConfigCardRenderer.TryCopyToClipboard(name, _snapshot))
        {
            MessageBox.Show("Impossible de copier la carte.", "Mon PC", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show("Carte copiée dans le presse-papiers — colle-la sur Discord.", "Mon PC",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return "0 Go";

        var gb = bytes / (1024d * 1024 * 1024);
        return $"{gb:0.#} Go";
    }
}
