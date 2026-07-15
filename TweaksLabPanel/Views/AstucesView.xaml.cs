using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TweaksLabPanel.Core;
using TweaksLabPanel.Services;

namespace TweaksLabPanel.Views;

public partial class AstucesView : UserControl
{
    private readonly Dictionary<string, ToggleButton> _startupToggles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _startupSyncing = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<StartupAppEntry> _startupEntries = [];

    private bool _syncingPowerToggle;
    private bool _syncingAppearanceToggle;

    public AstucesView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshUi();
    }

    private async void InstallPowerPlanButton_Click(object sender, RoutedEventArgs e)
    {
        if (TweaksLabPowerPlanService.IsInstalled())
            return;

        SetPowerBusy(true);
        HidePowerFeedback();

        var result = await TweaksLabPowerPlanService.InstallAsync();
        SetPowerBusy(false);

        if (result.Success)
        {
            ShowPowerFeedback("Plan installé et activé.", success: true);
            RefreshUi();
            return;
        }

        ShowPowerFeedback(result.ErrorMessage ?? "Impossible d'installer le plan.", success: false);
    }

    private async void PowerPlanToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncingPowerToggle)
            return;

        if (!TweaksLabPowerPlanService.IsInstalled())
        {
            ResetPowerToggle(false);
            ShowPowerFeedback("Installe d'abord le plan TweaksLab Community.", success: false);
            return;
        }

        SetPowerToggleEnabled(false);
        HidePowerFeedback();

        var result = await TweaksLabPowerPlanService.ActivateAsync();
        SetPowerToggleEnabled(true);

        if (result.Success)
        {
            RefreshUi();
            return;
        }

        ResetPowerToggle(false);
        ShowPowerFeedback(result.ErrorMessage ?? "Impossible d'activer le plan.", success: false);
        RefreshUi();
    }

    private async void PowerPlanToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_syncingPowerToggle)
            return;

        if (!TweaksLabPowerPlanService.IsInstalled())
        {
            ResetPowerToggle(false);
            return;
        }

        SetPowerToggleEnabled(false);
        HidePowerFeedback();

        var result = await TweaksLabPowerPlanService.DeactivateAsync();
        SetPowerToggleEnabled(true);

        if (result.Success)
        {
            RefreshUi();
            return;
        }

        ResetPowerToggle(true);
        ShowPowerFeedback(result.ErrorMessage ?? "Impossible de désactiver le plan.", success: false);
        RefreshUi();
    }

    private async void AppearanceToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncingAppearanceToggle)
            return;

        DiagnosticLog.Write("AppearanceUI", "Toggle Checked (activation demandée)");
        SetAppearanceToggleEnabled(false);
        HideAppearanceFeedback();

        var result = await WindowsAppearanceService.EnableAsync();
        SetAppearanceToggleEnabled(true);

        if (result.Success)
        {
            DiagnosticLog.Write("AppearanceUI", "Toggle Checked terminé avec succès");
            RefreshUi();
            return;
        }

        DiagnosticLog.Write("AppearanceUI", $"Toggle Checked échec: {result.ErrorMessage}");
        ResetAppearanceToggle(false);
        ShowAppearanceFeedback(
            $"{result.ErrorMessage ?? "Impossible d'appliquer les réglages."}\nLogs: {WindowsAppearanceService.LogDirectoryPath}",
            success: false);
        RefreshUi();
    }

    private async void AppearanceToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_syncingAppearanceToggle)
            return;

        DiagnosticLog.Write("AppearanceUI", "Toggle Unchecked (désactivation demandée)");
        SetAppearanceToggleEnabled(false);
        HideAppearanceFeedback();

        var result = await WindowsAppearanceService.DisableAsync();
        SetAppearanceToggleEnabled(true);

        if (result.Success)
        {
            DiagnosticLog.Write("AppearanceUI", "Toggle Unchecked terminé avec succès");
            RefreshUi();
            return;
        }

        DiagnosticLog.Write("AppearanceUI", $"Toggle Unchecked échec: {result.ErrorMessage}");
        ResetAppearanceToggle(true);
        ShowAppearanceFeedback(
            $"{result.ErrorMessage ?? "Impossible de restaurer les réglages."}\nLogs: {WindowsAppearanceService.LogDirectoryPath}",
            success: false);
        RefreshUi();
    }

    private async void StartupToggle_Changed(StartupAppEntry entry, ToggleButton toggle, bool enabled)
    {
        if (_startupSyncing.TryGetValue(entry.Id, out var syncing) && syncing)
            return;

        _startupSyncing[entry.Id] = true;
        toggle.IsEnabled = false;
        HideStartupFeedback();

        var result = await Task.Run(() => StartupAppsService.SetEnabled(entry, enabled));

        _startupSyncing[entry.Id] = false;
        toggle.IsEnabled = entry.CanToggle;

        if (result.Success)
        {
            RefreshStartupUi();
            return;
        }

        _startupSyncing[entry.Id] = true;
        toggle.IsChecked = !enabled;
        _startupSyncing[entry.Id] = false;
        ShowStartupFeedback(result.ErrorMessage ?? "Impossible de modifier cette application.", success: false);
        RefreshStartupUi();
    }

    private void RefreshUi()
    {
        RefreshPowerUi();
        RefreshAppearanceUi();
        RefreshStartupUi();
    }

    private void RefreshPowerUi()
    {
        var installed = TweaksLabPowerPlanService.IsInstalled();
        var active = TweaksLabPowerPlanService.IsActive();

        PowerStatusText.Text = active
            ? "Actif"
            : installed
                ? "Installé"
                : "Non installé";

        PowerStatusText.Foreground = active
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("TextLightGrayBrush");

        InstallPowerPlanButton.Content = "Installer le plan";
        InstallPowerPlanButton.Visibility = installed ? Visibility.Collapsed : Visibility.Visible;
        ResetPowerToggle(active);
        PowerPlanToggle.IsEnabled = installed;
        PowerToggleHint.Text = installed
            ? "Bascule vers le plan TweaksLab Community ou revient au plan précédent."
            : "Installe d'abord le plan pour pouvoir l'activer.";
    }

    private void RefreshAppearanceUi()
    {
        WindowsAppearanceService.SyncStoredState();
        var storedActive = WindowsAppearanceState.Active;
        var active = WindowsAppearanceService.IsActive();

        DiagnosticLog.Write("AppearanceUI",
            $"RefreshUi storedActive={storedActive} uiActive={active} toggle={AppearanceToggle.IsChecked == true}");

        AppearanceStatusText.Text = active ? "Actif" : "Inactif";
        AppearanceStatusText.Foreground = active
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("TextLightGrayBrush");

        AppearanceLogHintText.Text = $"Logs locaux : {WindowsAppearanceService.LogDirectoryPath}";
        ResetAppearanceToggle(active);
    }

    private void RefreshStartupUi()
    {
        _startupEntries = StartupAppsService.Scan();
        var enabledCount = StartupAppsService.CountEnabled(_startupEntries);
        var totalCount = _startupEntries.Count;

        StartupCountText.Text = totalCount == 0
            ? "Aucune application"
            : $"{enabledCount} active{(enabledCount > 1 ? "s" : "")} sur {totalCount}";

        StartupAdviceText.Text = enabledCount switch
        {
            0 => "Parfait — aucune application ne se lance au démarrage. Ton PC démarrera plus vite.",
            <= 5 => "C'est bien. Garde uniquement les applications vraiment utiles au démarrage.",
            <= 10 => "Tu as plusieurs applications au démarrage. Désactive celles que tu n'utilises pas tous les jours pour gagner en performance.",
            _ => "Beaucoup d'applications se lancent au démarrage — c'est important de les réduire pour de meilleures performances et moins de lag.",
        };

        if (totalCount == 0)
        {
            StartupAppsExpander.Visibility = Visibility.Collapsed;
            StartupEmptyText.Visibility = Visibility.Visible;
            StartupAppsList.Children.Clear();
            _startupToggles.Clear();
            return;
        }

        StartupEmptyText.Visibility = Visibility.Collapsed;
        StartupAppsExpander.Visibility = Visibility.Visible;
        StartupExpanderTitle.Text = $"Voir les applications ({totalCount})";

        BuildStartupList();
    }

    private void BuildStartupList()
    {
        StartupAppsList.Children.Clear();
        _startupToggles.Clear();

        foreach (var entry in _startupEntries)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock
            {
                Text = entry.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = (Brush)FindResource("TextWhiteBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            };

            Grid.SetColumn(name, 0);
            grid.Children.Add(name);

            var toggle = new ToggleButton
            {
                Style = (Style)FindResource("AccentToggle"),
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = entry.IsEnabled,
                IsEnabled = entry.CanToggle,
                Tag = entry,
            };

            toggle.Checked += (_, _) =>
            {
                if (toggle.Tag is StartupAppEntry tagged)
                    StartupToggle_Changed(tagged, toggle, true);
            };
            toggle.Unchecked += (_, _) =>
            {
                if (toggle.Tag is StartupAppEntry tagged)
                    StartupToggle_Changed(tagged, toggle, false);
            };

            Grid.SetColumn(toggle, 1);
            grid.Children.Add(toggle);

            StartupAppsList.Children.Add(grid);
            _startupToggles[entry.Id] = toggle;
        }
    }

    private void ResetPowerToggle(bool isChecked)
    {
        _syncingPowerToggle = true;
        PowerPlanToggle.IsChecked = isChecked;
        _syncingPowerToggle = false;
    }

    private void ResetAppearanceToggle(bool isChecked)
    {
        _syncingAppearanceToggle = true;
        AppearanceToggle.IsChecked = isChecked;
        _syncingAppearanceToggle = false;
    }

    private void SetPowerBusy(bool busy)
    {
        var installed = TweaksLabPowerPlanService.IsInstalled();
        InstallPowerPlanButton.IsEnabled = !busy && !installed;
        PowerPlanToggle.IsEnabled = !busy && installed;
    }

    private void SetPowerToggleEnabled(bool enabled)
        => PowerPlanToggle.IsEnabled = enabled && TweaksLabPowerPlanService.IsInstalled();

    private void SetAppearanceToggleEnabled(bool enabled)
        => AppearanceToggle.IsEnabled = enabled;

    private void ShowPowerFeedback(string message, bool success)
    {
        PowerFeedbackText.Text = message;
        PowerFeedbackText.Foreground = success
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("RedAccentBrush");
        PowerFeedbackText.Visibility = Visibility.Visible;
    }

    private void HidePowerFeedback() => PowerFeedbackText.Visibility = Visibility.Collapsed;

    private void ShowAppearanceFeedback(string message, bool success)
    {
        AppearanceFeedbackText.Text = message;
        AppearanceFeedbackText.Foreground = success
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("RedAccentBrush");
        AppearanceFeedbackText.Visibility = Visibility.Visible;
    }

    private void HideAppearanceFeedback() => AppearanceFeedbackText.Visibility = Visibility.Collapsed;

    private void ShowStartupFeedback(string message, bool success)
    {
        StartupFeedbackText.Text = message;
        StartupFeedbackText.Foreground = success
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("RedAccentBrush");
        StartupFeedbackText.Visibility = Visibility.Visible;
    }

    private void HideStartupFeedback() => StartupFeedbackText.Visibility = Visibility.Collapsed;

    public void ScrollToTop() => PageScroll.ScrollToVerticalOffset(0);

    public FrameworkElement? GetOnboardingTarget(int step) => step switch
    {
        0 => OnboardingPowerPlanHeader,
        _ => null,
    };
}
