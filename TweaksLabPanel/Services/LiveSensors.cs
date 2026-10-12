namespace TweaksLabPanel.Services;

public sealed record LiveSensorReading(
    double? CpuTempC,
    double? GpuTempC,
    double? CpuUsagePercent,
    double RamUsedGb,
    double RamTotalGb);

/// <summary>
/// Relevé en direct des capteurs (toutes les 3 s) tant qu'au moins une page l'écoute.
/// Les abonnés sont appelés depuis un thread de fond.
/// </summary>
public static class LiveSensors
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);
    private static readonly object Gate = new();
    private static readonly List<Action<LiveSensorReading>> Handlers = [];
    private static CancellationTokenSource? _cts;

    public static LiveSensorReading? Latest { get; private set; }

    public static void Subscribe(Action<LiveSensorReading> handler)
    {
        lock (Gate)
        {
            if (Handlers.Contains(handler))
                return;

            Handlers.Add(handler);
            if (Handlers.Count == 1)
                Start();
        }

        if (Latest is { } latest)
            handler(latest);
    }

    public static void Unsubscribe(Action<LiveSensorReading> handler)
    {
        lock (Gate)
        {
            if (!Handlers.Remove(handler) || Handlers.Count > 0)
                return;

            _cts?.Cancel();
            _cts = null;
        }
    }

    public static LiveSensorReading ReadNow()
    {
        var (usedGb, totalGb) = HardwareSensors.ReadRam();
        var reading = new LiveSensorReading(
            HardwareSensors.ReadCpuTemperatureC(),
            HardwareSensors.ReadGpuTemperatureC(),
            HardwareSensors.SampleCpuUsagePercent(),
            usedGb,
            totalGb);
        Latest = reading;
        return reading;
    }

    private static void Start()
    {
        var cts = new CancellationTokenSource();
        _cts = cts;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var reading = ReadNow();
                    Action<LiveSensorReading>[] snapshot;
                    lock (Gate)
                        snapshot = Handlers.ToArray();

                    foreach (var handler in snapshot)
                        handler(reading);
                }
                catch
                {
                    // Un capteur qui échoue ne doit pas arrêter le relevé.
                }

                try
                {
                    await Task.Delay(Interval, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, token);
    }
}
