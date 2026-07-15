using TweaksLabPanel.Core;

namespace TweaksLabPanel.Services;

public static class PerformanceScoreService
{
    public static event Action? ScoreUpdated;

    public static bool IsScanning { get; private set; }
    public static bool HasScan => PerformanceScoreStore.Instance.HasScan;
    public static int Score => PerformanceScoreStore.Instance.Score;
    public static PerformanceSnapshot? Snapshot => PerformanceScoreStore.Instance.Snapshot;

    public static void Initialize() => PerformanceScoreStore.Instance.Load();

    public static async Task EnsureScannedAsync()
    {
        if (PerformanceScoreStore.Instance.HasScan || IsScanning)
            return;

        IsScanning = true;
        try
        {
            var snapshot = await Task.Run(SystemPerformanceScanner.Scan);
            var score = PerformanceScoreCalculator.Calculate(snapshot);
            PerformanceScoreStore.Instance.Save(score, snapshot);
            ScoreUpdated?.Invoke();
        }
        finally
        {
            IsScanning = false;
        }
    }
}
