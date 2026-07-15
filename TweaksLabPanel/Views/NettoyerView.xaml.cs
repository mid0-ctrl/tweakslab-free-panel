using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TweaksLabPanel.Services;

namespace TweaksLabPanel.Views;

public partial class NettoyerView : UserControl
{
    private static readonly string[] LoadingSteps =
    [
        "Analyse des fichiers temporaires...",
        "Nettoyage de la corbeille...",
        "Suppression du cache système...",
        "Optimisation des dossiers Windows...",
        "Finalisation du nettoyage...",
    ];

    private bool _isCleaning;

    public NettoyerView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshEstimate();
    }

    private void RefreshEstimate()
    {
        var estimate = DiskCleanupScanner.Scan();
        EstimateText.Text = $"~{estimate.TotalGb:0.#} Go";
        DetailText.Text =
            $"Temp utilisateur : {FormatMb(estimate.TempUserBytes)}\n" +
            $"Temp Windows : {FormatMb(estimate.TempWindowsBytes)}\n" +
            $"Corbeille : {FormatMb(estimate.RecycleBinBytes)}";
    }

    private async void CleanAll_Click(object sender, RoutedEventArgs e)
    {
        await RunCleaningWithLoadingAsync(
            DiskCleanupService.CleanAll,
            freed => $"Nettoyage terminé — environ {freed.TotalGb:0.#} Go libérés.");
    }

    private async void Tournament_Click(object sender, RoutedEventArgs e)
    {
        await RunCleaningWithLoadingAsync(
            () =>
            {
                DiskCleanupService.CleanTournament();
                return DiskCleanupScanner.Scan();
            },
            _ => "Mode Avant tournoi : corbeille et fichiers temporaires nettoyés.");
    }

    private async Task RunCleaningWithLoadingAsync(
        Func<CleanupEstimate> cleanAction,
        Func<CleanupEstimate, string> buildResultMessage)
    {
        if (_isCleaning)
            return;

        _isCleaning = true;
        SetButtonsEnabled(false);
        ResultText.Text = string.Empty;
        LoadingPanel.Visibility = Visibility.Visible;
        CleanProgress.Value = 0;
        LoadingPercent.Text = "0 %";
        LoadingStatus.Text = LoadingSteps[0];

        var duration = TimeSpan.FromMilliseconds(Random.Shared.Next(15_000, 30_001));
        var startedAt = DateTime.UtcNow;
        var cleanTask = Task.Run(cleanAction);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        timer.Tick += (_, _) =>
        {
            var elapsed = DateTime.UtcNow - startedAt;
            var ratio = Math.Min(1, elapsed.TotalMilliseconds / duration.TotalMilliseconds);
            var progress = ratio * 100;

            CleanProgress.Value = progress;
            LoadingPercent.Text = $"{progress:0} %";

            var stepIndex = Math.Min(LoadingSteps.Length - 1, (int)(ratio * LoadingSteps.Length));
            LoadingStatus.Text = LoadingSteps[stepIndex];
        };
        timer.Start();

        try
        {
            await Task.WhenAll(cleanTask, Task.Delay(duration));
            var freed = await cleanTask;
            ResultText.Text = buildResultMessage(freed);
            RefreshEstimate();
        }
        finally
        {
            timer.Stop();
            CleanProgress.Value = 100;
            LoadingPercent.Text = "100 %";
            LoadingStatus.Text = "Terminé.";
            await Task.Delay(350);
            LoadingPanel.Visibility = Visibility.Collapsed;
            SetButtonsEnabled(true);
            _isCleaning = false;
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        CleanBtn.IsEnabled = enabled;
        TournamentBtn.IsEnabled = enabled;
    }

    private static string FormatMb(long bytes)
    {
        if (bytes <= 0)
            return "0 Mo";
        return $"{bytes / (1024d * 1024):0.#} Mo";
    }
}
