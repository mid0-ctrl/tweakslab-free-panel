using TweaksLabPanel.Core;

namespace TweaksLabPanel.Services;

public static class PerformanceScoreService
{
    public static event Action? ScoreUpdated;

    /// <summary>Une analyse démarre ou se termine (pour afficher « Analyse en cours… »).</summary>
    public static event Action? ScanStateChanged;

    private static bool _rescanRequested;

    public static bool IsScanning { get; private set; }
    public static bool HasScan => PerformanceScoreStore.Instance.HasScan;
    public static PerformanceSnapshot? Snapshot => PerformanceScoreStore.Instance.Snapshot;

    /// <summary>Détail du score, recalculé à partir des mesures enregistrées.</summary>
    public static ScoreBreakdown? Breakdown =>
        Snapshot is { } snapshot ? PerformanceScoreCalculator.Evaluate(snapshot) : null;

    /// <summary>Score de santé de 0 à 90.</summary>
    public static int Score => Breakdown?.Score ?? 0;

    public static void Initialize() => PerformanceScoreStore.Instance.Load();

    /// <summary>
    /// Nouvelle analyse complète (à chaque lancement, sur « Recalculer », après une astuce ou un nettoyage).
    /// Si une analyse tourne déjà, une nouvelle est relancée juste après pour prendre en compte le dernier changement.
    /// </summary>
    public static async Task ScanAsync()
    {
        if (IsScanning)
        {
            _rescanRequested = true;
            return;
        }

        IsScanning = true;
        ScanStateChanged?.Invoke();
        try
        {
            do
            {
                _rescanRequested = false;
                var snapshot = await Task.Run(SystemPerformanceScanner.Scan);
                PerformanceScoreStore.Instance.Save(snapshot);
                ScoreUpdated?.Invoke();
            }
            while (_rescanRequested);
        }
        catch
        {
            // Le dernier scan enregistré reste affiché.
        }
        finally
        {
            IsScanning = false;
            ScanStateChanged?.Invoke();
        }
    }

    public static Task EnsureScannedAsync() => HasScan ? Task.CompletedTask : ScanAsync();

    /// <summary>Texte sous l'anneau, selon le score.</summary>
    public static string BandLabel(int score) => score switch
    {
        < 50 => Helpers.LocalizationService.T("À optimiser en priorité", "Needs optimizing first"),
        < 75 => Helpers.LocalizationService.T("Peut gagner en performance", "Room to gain performance"),
        _ => Helpers.LocalizationService.T("En bonne forme", "In good shape"),
    };
}
