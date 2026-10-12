using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Services;

public sealed record ChatCta(string Label, string Url);

public sealed record ChatResult(string Reply, ChatCta? Cta, ChatCta? TicketCta);

public enum ChatFailure
{
    None,
    Unreachable,
    RateLimited,

    /// <summary>Requête refusée par le bot (400 / 413 / 422) : on peut retenter sans le contexte PC.</summary>
    Rejected,
}

public sealed record ChatOutcome(ChatResult? Result, ChatFailure Failure);

public static class ChatService
{
    /// <summary>Longueur max d'un message (anti-abus et coût).</summary>
    public const int MaxMessageLength = 500;

    /// <summary>Nombre de messages d'historique renvoyés au bot (limite le coût de chaque appel).</summary>
    private const int HistoryWindow = 10;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(90),
    };

    private static readonly string SessionId = LoadOrCreateSessionId();

    /// <param name="pcContext">Infos du PC envoyées en début de conversation pour personnaliser les réponses (peut être null).</param>
    public static async Task<ChatOutcome> SendAsync(
        string message,
        IReadOnlyList<ChatMessage> history,
        string? pcContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new ChatOutcome(null, ChatFailure.Unreachable);

        var trimmed = message.Trim();
        if (trimmed.Length > MaxMessageLength)
            trimmed = trimmed[..MaxMessageLength];

        var recent = history.TakeLast(HistoryWindow).ToList();
        var withContext = !string.IsNullOrWhiteSpace(pcContext);

        var outcome = await PostAsync(trimmed, BuildHistory(recent, withContext ? pcContext : null), cancellationToken);

        // Si le bot refuse l'historique enrichi, on retente une fois sans le contexte PC.
        if (outcome.Failure == ChatFailure.Rejected && withContext && !cancellationToken.IsCancellationRequested)
            outcome = await PostAsync(trimmed, BuildHistory(recent, null), cancellationToken);

        return outcome.Failure == ChatFailure.Rejected
            ? outcome with { Failure = ChatFailure.Unreachable }
            : outcome;
    }

    private static List<ChatRequestMessage> BuildHistory(IReadOnlyList<ChatMessage> recent, string? pcContext)
    {
        var list = new List<ChatRequestMessage>();
        if (!string.IsNullOrWhiteSpace(pcContext))
        {
            list.Add(new ChatRequestMessage { Role = "user", Content = pcContext });
            list.Add(new ChatRequestMessage
            {
                Role = "assistant",
                Content = "Bien reçu, je garde ces infos sur ton PC en tête pour te conseiller.",
            });
        }

        list.AddRange(recent.Select(h => new ChatRequestMessage { Role = h.Role, Content = h.Content }));
        return list;
    }

    private static async Task<ChatOutcome> PostAsync(
        string message,
        List<ChatRequestMessage> history,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{PanelEndpoints.ChatBaseUrl.TrimEnd('/')}{PanelEndpoints.ChatPanelPath}/chat";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("User-Agent", "TweaksLabPanel/1.0");
            request.Headers.TryAddWithoutValidation("X-Panel-Session", SessionId);

            if (!string.IsNullOrWhiteSpace(PanelEndpoints.PanelChatApiKey))
                request.Headers.TryAddWithoutValidation("X-Panel-Key", PanelEndpoints.PanelChatApiKey);

            var (timestamp, nonce, signature) = PanelChatSecurity.BuildSignedHeaders(SessionId, message);
            request.Headers.TryAddWithoutValidation("X-Panel-Ts", timestamp);
            request.Headers.TryAddWithoutValidation("X-Panel-Nonce", nonce);
            request.Headers.TryAddWithoutValidation("X-Panel-Sig", signature);

            var payload = new ChatRequest { Message = message, History = history };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await Http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return new ChatOutcome(null, ChatFailure.RateLimited);

            // Format refusé (historique enrichi trop long ou non accepté) : on pourra retenter sans le contexte PC.
            if ((int)response.StatusCode is 400 or 413 or 422)
                return new ChatOutcome(null, ChatFailure.Rejected);

            if (!response.IsSuccessStatusCode)
                return new ChatOutcome(null, ChatFailure.Unreachable);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<ChatResponse>(stream, cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(result?.Reply))
                return new ChatOutcome(null, ChatFailure.Unreachable);

            return new ChatOutcome(
                new ChatResult(result.Reply.Trim(), MapCta(result.Cta), MapCta(result.TicketCta)),
                ChatFailure.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new ChatOutcome(null, ChatFailure.Unreachable);
        }
    }

    private static ChatCta? MapCta(ChatCtaDto? dto) =>
        dto is null || string.IsNullOrWhiteSpace(dto.Url) || string.IsNullOrWhiteSpace(dto.Label)
            ? null
            : ChatUrlAllowlist.IsAllowed(dto.Url)
                ? new ChatCta(dto.Label.Trim(), dto.Url.Trim())
                : null;

    private static string LoadOrCreateSessionId()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TweaksLabPanel",
            "chat-session.txt");
        try
        {
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (!string.IsNullOrEmpty(existing) && existing.Length >= 16)
                    return existing;
            }

            var id = Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, id);
            return id;
        }
        catch
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    public static void OpenUrl(string url)
    {
        ChatUrlAllowlist.TryOpen(url);
    }

    private sealed class ChatRequest
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("history")]
        public List<ChatRequestMessage> History { get; set; } = [];
    }

    private sealed class ChatRequestMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("reply")]
        public string? Reply { get; set; }

        [JsonPropertyName("cta")]
        public ChatCtaDto? Cta { get; set; }

        [JsonPropertyName("ticketCta")]
        public ChatCtaDto? TicketCta { get; set; }
    }

    private sealed class ChatCtaDto
    {
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
