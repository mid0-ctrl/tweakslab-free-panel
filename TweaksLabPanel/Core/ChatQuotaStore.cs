using System.IO;
using System.Text.Json;

namespace TweaksLabPanel.Core;

/// <summary>
/// Limite locale du chat : nombre de messages par jour et délai entre deux messages.
/// Le serveur du bot applique aussi ses propres limites (par session, par IP et globales).
/// </summary>
public sealed class ChatQuotaStore
{
    public const int DailyLimit = 30;
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(4);

    public static ChatQuotaStore Instance { get; } = new();

    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TweaksLabPanel",
        "chat-quota.json");

    private readonly object _gate = new();
    private DateOnly _day;
    private int _count;
    private DateTime _lastSentUtc;
    private bool _loaded;

    public int RemainingToday
    {
        get
        {
            lock (_gate)
            {
                EnsureLoaded();
                RollDay();
                return Math.Max(0, DailyLimit - _count);
            }
        }
    }

    public TimeSpan CooldownRemaining
    {
        get
        {
            lock (_gate)
            {
                var remaining = Cooldown - (DateTime.UtcNow - _lastSentUtc);
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }
    }

    /// <summary>Réserve un message ; false si la limite du jour est atteinte.</summary>
    public bool TryConsume()
    {
        lock (_gate)
        {
            EnsureLoaded();
            RollDay();
            if (_count >= DailyLimit)
                return false;

            _count++;
            _lastSentUtc = DateTime.UtcNow;
            Persist();
            return true;
        }
    }

    /// <summary>Rend un message au quota quand l'envoi a échoué (hors ligne, serveur saturé…).</summary>
    public void Refund()
    {
        lock (_gate)
        {
            EnsureLoaded();
            if (_count <= 0)
                return;

            _count--;
            Persist();
        }
    }

    private void RollDay()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (_day == today)
            return;

        _day = today;
        _count = 0;
    }

    private void EnsureLoaded()
    {
        if (_loaded)
            return;

        _loaded = true;
        try
        {
            if (!File.Exists(_path))
                return;

            var data = JsonSerializer.Deserialize<QuotaData>(File.ReadAllText(_path));
            if (data is null)
                return;

            _day = data.Day;
            _count = Math.Max(0, data.Count);
        }
        catch
        {
            // Fichier illisible : on repart de zéro.
        }
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(new QuotaData { Day = _day, Count = _count }));
        }
        catch
        {
            // Ignorer.
        }
    }

    private sealed class QuotaData
    {
        public DateOnly Day { get; set; }
        public int Count { get; set; }
    }
}
