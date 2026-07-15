using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;
using TweaksLabPanel.Services;

namespace TweaksLabPanel.Views;

public partial class ParametresView : UserControl
{
    private IReadOnlyList<DisplayModeInfo> _modes = [];
    private DisplayModeInfo? _current;

    public ParametresView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RefreshDisplaySettings();
            RefreshBiosFeatures();
        };
        LocalizationService.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged() => ApplyLocalizedText();

    private void ApplyLocalizedText()
    {
        BiosSectionTitle.Text = LocalizationService.BiosSection;
        BiosSectionHint.Text = LocalizationService.BiosSectionHint;
        TpmLabelText.Text = LocalizationService.TpmLabel;
        SecureBootLabelText.Text = LocalizationService.SecureBootLabel;
        XmpLabelText.Text = LocalizationService.XmpLabel;
        XmpTutorialLink.Text = LocalizationService.XmpTutorialLink;
        RebootBiosBtnText.Text = LocalizationService.RebootBiosButton;
        OnboardingSectionTitle.Text = LocalizationService.OnboardingSection;
        OnboardingSectionHint.Text = LocalizationService.OnboardingSectionHint;
        ReplayOnboardingBtnText.Text = LocalizationService.ReplayOnboardingButton;
    }

    public void RefreshBiosFeatures()
    {
        ApplyLocalizedText();
        var snapshot = BiosFeaturesScanner.Scan();
        ApplyStatus(TpmStatusText, snapshot.Tpm);
        ApplyStatus(SecureBootStatusText, snapshot.SecureBoot);
        ApplyStatus(XmpStatusText, snapshot.Xmp, snapshot.XmpDetail);
        XmpTutorialLink.Visibility = snapshot.Xmp == FeatureActivationState.Disabled
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void XmpTutorialLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo(PanelEndpoints.XmpTutorialUrl) { UseShellExecute = true });
    }

    private static void ApplyStatus(TextBlock target, FeatureActivationState state, string? detail = null)
    {
        target.Text = state switch
        {
            FeatureActivationState.Enabled => LocalizationService.StatusEnabled,
            FeatureActivationState.Disabled => LocalizationService.StatusDisabled,
            FeatureActivationState.NotSupported => LocalizationService.StatusNotSupported,
            _ => LocalizationService.StatusUnknown,
        };

        if (!string.IsNullOrWhiteSpace(detail) && state != FeatureActivationState.NotSupported)
            target.Text += $" · {detail}";

        target.Foreground = state switch
        {
            FeatureActivationState.Enabled => (Brush)target.FindResource("GreenAccentBrush"),
            FeatureActivationState.Disabled => (Brush)target.FindResource("RedAccentBrush"),
            _ => (Brush)target.FindResource("TextGrayBrush"),
        };
    }

    private void RebootBios_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            LocalizationService.RebootBiosConfirm,
            LocalizationService.RebootBiosTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        if (FirmwareRebootService.TryRebootToFirmware(out var message))
            return;

        MessageBox.Show(message, LocalizationService.RebootBiosTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ReplayOnboarding_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow main)
            main.RestartOnboarding();
    }

    public void RefreshDisplaySettings()
    {
        _modes = DisplaySettingsService.GetSupportedModes();
        _current = DisplaySettingsService.GetCurrentMode();

        CurrentModeText.Text = _current is null
            ? "—"
            : $"{_current.ResolutionLabel} @ {_current.RefreshHz} Hz";

        var resolutions = _modes
            .Select(m => m.ResolutionLabel)
            .Distinct()
            .ToList();

        ResolutionCombo.Items.Clear();
        foreach (var res in resolutions)
            ResolutionCombo.Items.Add(res);

        if (_current is not null)
            ResolutionCombo.SelectedItem = _current.ResolutionLabel;
        else if (ResolutionCombo.Items.Count > 0)
            ResolutionCombo.SelectedIndex = 0;

        RefreshRefreshRates();
        LanguageCombo.SelectedIndex = AppSettingsStore.Instance.Language == "en" ? 1 : 0;
        KeyboardCombo.SelectedIndex = AppSettingsStore.Instance.KeyboardLayout == "qwerty" ? 1 : 0;
    }

    private void ResolutionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshRefreshRates();

    private void RefreshRefreshRates()
    {
        RefreshCombo.Items.Clear();
        if (ResolutionCombo.SelectedItem is not string resolution)
            return;

        var rates = _modes
            .Where(m => m.ResolutionLabel == resolution)
            .Select(m => m.RefreshHz)
            .Distinct()
            .OrderByDescending(r => r);

        foreach (var rate in rates)
            RefreshCombo.Items.Add($"{rate} Hz");

        if (_current is not null && _current.ResolutionLabel == resolution)
            RefreshCombo.SelectedItem = $"{_current.RefreshHz} Hz";
        else if (RefreshCombo.Items.Count > 0)
            RefreshCombo.SelectedIndex = 0;
    }

    private void ApplyDisplay_Click(object sender, RoutedEventArgs e)
    {
        if (ResolutionCombo.SelectedItem is not string resolution ||
            RefreshCombo.SelectedItem is not string refreshLabel)
            return;

        var hz = int.Parse(refreshLabel.Replace(" Hz", "", StringComparison.Ordinal));
        var mode = _modes.FirstOrDefault(m => m.ResolutionLabel == resolution && m.RefreshHz == hz);
        if (mode is null)
            return;

        var confirm = MessageBox.Show(
            LocalizationService.ConfirmDisplayChange,
            LocalizationService.DisplaySection,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        if (DisplaySettingsService.TryApply(mode, out var message))
        {
            MessageBox.Show(message, LocalizationService.DisplaySection, MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshDisplaySettings();
        }
        else
        {
            MessageBox.Show(message, LocalizationService.DisplaySection, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || LanguageCombo.SelectedIndex < 0)
            return;

        LocalizationService.SetLanguage(LanguageCombo.SelectedIndex == 1 ? "en" : "fr");
    }

    private void KeyboardCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || KeyboardCombo.SelectedIndex < 0)
            return;

        AppSettingsStore.Instance.SaveKeyboardLayout(KeyboardCombo.SelectedIndex == 1 ? "qwerty" : "azerty");
    }
}
