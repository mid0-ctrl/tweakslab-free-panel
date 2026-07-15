using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Services;

public sealed record ChatCta(string Label, string Url);

public sealed record ChatResult(string Reply, ChatCta? Cta, ChatCta? TicketCta);

public static class ChatService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(90),
    };

    private static readonly string SessionId = LoadOrCreateSessionId();

    public static async Task<ChatResult?> SendAsync(
        string message,
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var url = $"{PanelEndpoints.ChatBaseUrl.TrimEnd('/')}{PanelEndpoints.ChatPanelPath}/chat";
        var trimmed = message.Trim();
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", "TweaksLabPanel/1.0");
        request.Headers.TryAddWithoutValidation("X-Panel-Session", SessionId);

        if (!string.IsNullOrWhiteSpace(PanelEndpoints.PanelChatApiKey))
            request.Headers.TryAddWithoutValidation("X-Panel-Key", PanelEndpoints.PanelChatApiKey);

        var (timestamp, nonce, signature) = PanelChatSecurity.BuildSignedHeaders(SessionId, trimmed);
        request.Headers.TryAddWithoutValidation("X-Panel-Ts", timestamp);
        request.Headers.TryAddWithoutValidation("X-Panel-Nonce", nonce);
        request.Headers.TryAddWithoutValidation("X-Panel-Sig", signature);

        var payload = new ChatRequest
        {
            Message = trimmed,
            History = history.Select(h => new ChatRequestMessage
            {
                Role = h.Role,
                Content = h.Content,
            }).ToList(),
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await Http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<ChatResponse>(stream, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(result?.Reply))
            return null;

        return new ChatResult(
            result.Reply.Trim(),
            MapCta(result.Cta),
            MapCta(result.TicketCta));
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
