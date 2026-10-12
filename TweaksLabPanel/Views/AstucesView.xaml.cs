using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;
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
        TipsOfferText.Text = LocalizationService.T(
            $"Tu as fait le plus simple. L’équipe TweaksLab peut optimiser ton PC en profondeur, {OfferLinks.StartingPrice}.",
            $"You’ve done the easy part. The TweaksLab team can optimize your PC in depth, {OfferLinks.StartingPrice}.");
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
            ShowPowerFeedback(LocalizationService.T("Plan installé et activé.", "Plan installed and turned on."), success: true);
            RefreshUi();
            RequestRescan();
            return;
        }

        ShowPowerFeedback(result.ErrorMessage ?? LocalizationService.T("Impossible d'installer le plan.", "Couldn't install the plan."), success: false);
    }

    private async void PowerPlanToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncingPowerToggle)
            return;

        if (!TweaksLabPowerPlanService.IsInstalled())
        {
            ResetPowerToggle(false);
            ShowPowerFeedback(LocalizationService.T("Installe d'abord le plan TweaksLab Community.", "Install the TweaksLab Community plan first."), success: false);
            return;
        }

        SetPowerToggleEnabled(false);
        HidePowerFeedback();

        var result = await TweaksLabPowerPlanService.ActivateAsync();
        SetPowerToggleEnabled(true);

        if (result.Success)
        {
            RefreshUi();
            RequestRescan();
            return;
        }

        ResetPowerToggle(false);
        ShowPowerFeedback(result.ErrorMessage ?? LocalizationService.T("Impossible d'activer le plan.", "Couldn't turn on the plan."), success: false);
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
            RequestRescan();
            return;
        }

        ResetPowerToggle(true);
        ShowPowerFeedback(result.ErrorMessage ?? LocalizationService.T("Impossible de désactiver le plan.", "Couldn't turn off the plan."), success: false);
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
            RequestRescan();
            return;
        }

        DiagnosticLog.Write("AppearanceUI", $"Toggle Checked échec: {result.ErrorMessage}");
        ResetAppearanceToggle(false);
        ShowAppearanceFeedback(
            result.ErrorMessage ?? LocalizationService.T("Impossible d'appliquer les réglages.", "Couldn't apply the settings."),
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
            RequestRescan();
            return;
        }

        DiagnosticLog.Write("AppearanceUI", $"Toggle Unchecked échec: {result.ErrorMessage}");
        ResetAppearanceToggle(true);
        ShowAppearanceFeedback(
            result.ErrorMessage ?? LocalizationService.T("Impossible de restaurer les réglages.", "Couldn't restore the settings."),
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
            RequestRescan();
            return;
        }

        _startupSyncing[entry.Id] = true;
        toggle.IsChecked = !enabled;
        _startupSyncing[entry.Id] = false;
        ShowStartupFeedback(result.ErrorMessage ?? LocalizationService.T("Impossible de modifier cette application.", "Couldn't change this app."), success: false);
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
            ? LocalizationService.T("Actif", "Active")
            : installed
                ? LocalizationService.T("Installé", "Installed")
                : LocalizationService.T("Non installé", "Not installed");

        PowerStatusText.Foreground = active
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("TextLightGrayBrush");

        InstallPowerPlanButton.Content = LocalizationService.T("Installer le plan", "Install the plan");
        InstallPowerPlanButton.Visibility = installed ? Visibility.Collapsed : Visibility.Visible;
        ResetPowerToggle(active);
        PowerPlanToggle.IsEnabled = installed;
        PowerToggleHint.Text = installed
            ? LocalizationService.T("Bascule vers le plan TweaksLab Community ou revient au plan précédent.", "Switch to the TweaksLab Community plan or go back to your previous plan.")
            : LocalizationService.T("Installe d'abord le plan pour pouvoir l'activer.", "Install the plan first to turn it on.");
    }

    private void RefreshAppearanceUi()
    {
        WindowsAppearanceService.SyncStoredState();
        var storedActive = WindowsAppearanceState.Active;
        var active = WindowsAppearanceService.IsActive();

        DiagnosticLog.Write("AppearanceUI",
            $"RefreshUi storedActive={storedActive} uiActive={active} toggle={AppearanceToggle.IsChecked == true}");

        AppearanceStatusText.Text = active ? LocalizationService.T("Actif", "Active") : LocalizationService.T("Inactif", "Inactive");
        AppearanceStatusText.Foreground = active
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("TextLightGrayBrush");

        ResetAppearanceToggle(active);
    }

    private void RefreshStartupUi()
    {
        _startupEntries = StartupAppsService.Scan();
        var enabledCount = StartupAppsService.CountEnabled(_startupEntries);
        var totalCount = _startupEntries.Count;

        StartupCountText.Text = totalCount == 0
            ? LocalizationService.T("Aucune application", "No apps")
            : LocalizationService.T(
                $"{enabledCount} active{(enabledCount > 1 ? "s" : "")} sur {totalCount}",
                $"{enabledCount} of {totalCount} enabled");

        StartupAdviceText.Text = enabledCount switch
        {
            0 => LocalizationService.T("Parfait — aucune application ne se lance au démarrage. Ton PC démarrera plus vite.", "Perfect — no apps launch at startup. Your PC will boot faster."),
            <= 5 => LocalizationService.T("C'est bien. Garde uniquement les applications vraiment utiles au démarrage.", "Good. Keep only the apps you really need at startup."),
            <= 10 => LocalizationService.T("Tu as plusieurs applications au démarrage. Désactive celles que tu n'utilises pas tous les jours pour gagner en performance.", "You have several apps at startup. Turn off the ones you don't use every day to gain performance."),
            _ => LocalizationService.T("Beaucoup d'applications se lancent au démarrage — c'est important de les réduire pour de meilleures performances et moins de lag.", "Lots of apps launch at startup — cutting them down means better performance and less lag."),
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
        StartupExpanderTitle.Text = LocalizationService.T($"Voir les applications ({totalCount})", $"See the apps ({totalCount})");

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

    /// <summary>Le score de santé est recalculé après chaque réglage appliqué (vraie mesure, pas de bonus).</summary>
    private static void RequestRescan() => _ = PerformanceScoreService.ScanAsync();

    private void TipsOffer_Click(object sender, RoutedEventArgs e) => OfferLinks.Open(OfferLinks.Tips);

    public void ScrollToTop() => PageScroll.ScrollToVerticalOffset(0);

    public FrameworkElement? GetOnboardingTarget(int step) => step switch
    {
        0 => OnboardingPowerPlanHeader,
        _ => null,
    };
}
