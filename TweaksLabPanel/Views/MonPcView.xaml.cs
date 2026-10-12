using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;
using TweaksLabPanel.Services;

namespace TweaksLabPanel.Views;

public partial class MonPcView : UserControl
{
    private readonly Action<LiveSensorReading> _liveSensorsHandler;
    private SystemSpecsSnapshot? _snapshot;
    private bool _refreshing;

    public MonPcView()
    {
        InitializeComponent();
        _liveSensorsHandler = reading => Dispatcher.BeginInvoke(() => ApplyTemperatures(reading.CpuTempC, reading.GpuTempC));
        Loaded += async (_, _) =>
        {
            LiveSensors.Subscribe(_liveSensorsHandler);
            await RefreshSpecsAsync();
        };
        Unloaded += (_, _) => LiveSensors.Unsubscribe(_liveSensorsHandler);
    }

    /// <summary>Lecture WMI en arrière-plan : la page ne fige plus à l'ouverture.</summary>
    public async Task RefreshSpecsAsync()
    {
        if (_refreshing)
            return;

        _refreshing = true;
        try
        {
            var snapshot = await Task.Run(SystemSpecsScanner.Scan);
            _snapshot = snapshot;
            ApplySnapshot(snapshot);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void ApplySnapshot(SystemSpecsSnapshot snapshot)
    {
        if (LiveSensors.Latest is null)
            ApplyTemperatures(snapshot.CpuTempC, snapshot.GpuTempC);

        OsText.Text = snapshot.OperatingSystem;
        CpuText.Text = $"{snapshot.Processor} ({snapshot.ProcessorDetails})";
        GpuText.Text = snapshot.GraphicsCard;
        RamText.Text = snapshot.Memory;
        BoardText.Text = snapshot.Motherboard;
        StorageText.Text = snapshot.Storage;

        var freePercent = snapshot.SystemDriveFreePercent;
        DiskBar.Value = freePercent;
        DiskPercentText.Text = $"{freePercent:0.#}%";
        DiskSpaceText.Text = LocalizationService.T(
            $"{FormatBytes(snapshot.SystemDriveFreeBytes)} libres sur {FormatBytes(snapshot.SystemDriveTotalBytes)}",
            $"{FormatBytes(snapshot.SystemDriveFreeBytes)} free of {FormatBytes(snapshot.SystemDriveTotalBytes)}");
        DiskWarningText.Visibility = freePercent < 10 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyTemperatures(double? cpuTempC, double? gpuTempC)
    {
        ApplyTemperature(CpuTempText, CpuTempNote, cpuTempC, LocalizationService.T(
            "Windows ne donne pas la température du processeur sur ce PC.",
            "Windows doesn't expose the CPU temperature on this PC."));
        ApplyTemperature(GpuTempText, GpuTempNote, gpuTempC, LocalizationService.T(
            "La carte graphique ne transmet pas sa température à Windows.",
            "The graphics card doesn't report its temperature to Windows."));
    }

    private void ApplyTemperature(TextBlock value, TextBlock note, double? tempC, string unavailableNote)
    {
        if (tempC is { } temp)
        {
            var approximate = value == CpuTempText && HardwareSensors.CpuTemperatureIsApproximate;
            value.Text = approximate ? $"~{temp:0}°C" : $"{temp:0}°C";
            value.Foreground = (Brush)FindResource("TextWhiteBrush");
            note.Text = LocalizationService.T("Capteur de la carte mère (approximatif).", "Motherboard sensor (approximate).");
            note.Visibility = approximate ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        value.Text = LocalizationService.T("N/D", "N/A");
        value.Foreground = (Brush)FindResource("TextGrayBrush");
        note.Text = unavailableNote;
        note.Visibility = Visibility.Visible;
    }

    private async Task<SystemSpecsSnapshot?> EnsureSnapshotAsync()
    {
        if (_snapshot is null)
            await RefreshSpecsAsync();
        return _snapshot;
    }

    private async void CopySpecs_Click(object sender, RoutedEventArgs e)
    {
        if (await EnsureSnapshotAsync() is not { } snapshot)
            return;

        try { Clipboard.SetText(SystemSpecsScanner.FormatSpecsText(snapshot).Trim()); } catch { }
    }

    private async void ShareSpecs_Click(object sender, RoutedEventArgs e)
    {
        if (await EnsureSnapshotAsync() is not { } snapshot)
            return;

        var name = UserProfileStore.Instance.ResolvedDisplayName;
        var title = LocalizationService.T("Mon PC", "My PC");

        if (!ShareConfigCardRenderer.TryCopyToClipboard(name, snapshot))
        {
            MessageBox.Show(LocalizationService.T("Impossible de copier la carte.", "Couldn't copy the card."),
                title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show(
            LocalizationService.T("Carte copiée dans le presse-papiers — colle-la sur Discord.", "Card copied to the clipboard — paste it on Discord."),
            title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string FormatBytes(long bytes)
    {
        var unit = LocalizationService.T("Go", "GB");
        if (bytes <= 0)
            return $"0 {unit}";

        var gb = bytes / (1024d * 1024 * 1024);
        return $"{gb:0.#} {unit}";
    }
}
