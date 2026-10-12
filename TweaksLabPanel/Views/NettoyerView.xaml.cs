using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TweaksLabPanel.Helpers;
using TweaksLabPanel.Services;

namespace TweaksLabPanel.Views;

public partial class NettoyerView : UserControl
{
    private bool _isCleaning;
    private bool _recycleBinWasEmpty;

    public NettoyerView()
    {
        InitializeComponent();
        CleanupOfferLink.Text = LocalizationService.T(
            $"Envie d’aller plus loin ? Découvre l’optimisation complète TweaksLab, {OfferLinks.StartingPrice} →",
            $"Want to go further? Discover the full TweaksLab optimization, {OfferLinks.StartingPrice} →");
        Loaded += async (_, _) => await RefreshEstimateAsync();
    }

    private async Task<CleanupEstimate> RefreshEstimateAsync()
    {
        var estimate = await Task.Run(DiskCleanupService.Estimate);
        EstimateText.Text = $"~{FormatSize(estimate.TotalBytes)}";

        StepsList.Children.Clear();
        foreach (var step in estimate.Steps)
            StepsList.Children.Add(BuildLine(DiskCleanupService.Label(step.Kind), FormatSize(step.Bytes), dim: step.Bytes == 0));

        return estimate;
    }

    private async void CleanAll_Click(object sender, RoutedEventArgs e) =>
        await RunCleaningAsync(tournament: false);

    private async void Tournament_Click(object sender, RoutedEventArgs e) =>
        await RunCleaningAsync(tournament: true);

    private async Task RunCleaningAsync(bool tournament)
    {
        if (_isCleaning)
            return;

        _isCleaning = true;
        SetButtonsEnabled(false);
        ResultCard.Visibility = Visibility.Collapsed;

        try
        {
            var estimate = await RefreshEstimateAsync();
            var recycleBin = estimate.For(CleanupStepKind.RecycleBin);
            _recycleBinWasEmpty = recycleBin.Items <= 0;
            var includeRecycleBin = AskRecycleBin(recycleBin);
            if (includeRecycleBin is null)
                return;

            LoadingPanel.Visibility = Visibility.Visible;
            CleanProgress.Value = 0;
            LoadingPercent.Text = "0 %";

            var progress = new Progress<CleanupProgress>(p =>
            {
                CleanProgress.Value = p.Percent;
                LoadingPercent.Text = $"{p.Percent:0} %";
                LoadingStatus.Text = p.StepIndex >= p.StepCount
                    ? LocalizationService.T("Terminé.", "Done.")
                    : $"{DiskCleanupService.Label(p.Step)}… ({p.StepIndex + 1}/{p.StepCount})";
            });

            var results = await Task.Run(() => DiskCleanupService.Run(includeRecycleBin.Value, progress));
            ShowResults(results, tournament);
            _ = PerformanceScoreService.ScanAsync();
            await RefreshEstimateAsync();
        }
        finally
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            SetButtonsEnabled(true);
            _isCleaning = false;
        }
    }

    /// <summary>true = vider la corbeille, false = nettoyer sans la corbeille, null = annuler.</summary>
    private static bool? AskRecycleBin(CleanupStepEstimate recycleBin)
    {
        // Corbeille vide : rien à vider (et rien ne sera supprimé sans avoir demandé).
        if (recycleBin.Items <= 0)
            return false;

        var answer = MessageBox.Show(
            LocalizationService.T(
                $"La corbeille contient {recycleBin.Items} élément(s) ({FormatSize(recycleBin.Bytes)}).\n\n" +
                "Ils seront supprimés définitivement et ne pourront plus être récupérés.\n\n" +
                "Oui : vider aussi la corbeille\nNon : nettoyer sans toucher à la corbeille\nAnnuler : ne rien faire",
                $"The Recycle Bin contains {recycleBin.Items} item(s) ({FormatSize(recycleBin.Bytes)}).\n\n" +
                "They will be permanently deleted and can't be recovered.\n\n" +
                "Yes: also empty the Recycle Bin\nNo: clean without touching the Recycle Bin\nCancel: do nothing"),
            LocalizationService.T("Vider la corbeille ?", "Empty the Recycle Bin?"),
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return answer switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null,
        };
    }

    private void ShowResults(IReadOnlyList<CleanupStepResult> results, bool tournament)
    {
        var freed = results.Sum(r => r.FreedBytes);
        ResultText.Text = tournament
            ? LocalizationService.T($"Prêt pour le tournoi : {FormatSize(freed)} libérés.", $"Match-ready: {FormatSize(freed)} freed.")
            : LocalizationService.T($"Nettoyage terminé : {FormatSize(freed)} libérés.", $"Cleanup done: {FormatSize(freed)} freed.");

        ResultDetails.Children.Clear();
        foreach (var result in results)
        {
            string value;
            if (result.Skipped && result.Kind == CleanupStepKind.RecycleBin && _recycleBinWasEmpty)
                value = LocalizationService.T("déjà vide", "already empty");
            else if (result.Skipped)
                value = LocalizationService.T("non vidée", "not emptied");
            else if (result.KeptFiles > 0)
                value = LocalizationService.T(
                    $"{FormatSize(result.FreedBytes)} · {result.KeptFiles} en cours d’utilisation",
                    $"{FormatSize(result.FreedBytes)} · {result.KeptFiles} in use");
            else
                value = FormatSize(result.FreedBytes);

            ResultDetails.Children.Add(BuildLine("✓ " + DiskCleanupService.Label(result.Kind), value, dim: result.Skipped));
        }

        ResultCard.Visibility = Visibility.Visible;
    }

    private Grid BuildLine(string label, string value, bool dim)
    {
        var brush = (Brush)FindResource(dim ? "TextGrayBrush" : "TextWhiteBrush");
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelText = new TextBlock { Text = label, FontSize = 12, Foreground = brush, TextTrimming = TextTrimming.CharacterEllipsis };
        var valueText = new TextBlock { Text = value, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = brush, Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(labelText);
        grid.Children.Add(valueText);
        return grid;
    }

    private void CleanupOffer_Click(object sender, MouseButtonEventArgs e) => OfferLinks.Open(OfferLinks.Cleanup);

    private void SetButtonsEnabled(bool enabled)
    {
        CleanBtn.IsEnabled = enabled;
        TournamentBtn.IsEnabled = enabled;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
            return LocalizationService.T($"{bytes / (1024d * 1024 * 1024):0.#} Go", $"{bytes / (1024d * 1024 * 1024):0.#} GB");

        return LocalizationService.T($"{bytes / (1024d * 1024):0.#} Mo", $"{bytes / (1024d * 1024):0.#} MB");
    }
}
