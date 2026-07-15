using System.Security.Cryptography;
using System.Text;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Services;

internal static class PanelChatSecurity
{
    private const string DeriveSalt = "tweakslab-panel-neuf";

    public static (string Timestamp, string Nonce, string Signature) BuildSignedHeaders(
        string sessionId,
        string message)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var nonce = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..16];
        var signature = Sign(sessionId, timestamp, nonce, message);
        return (timestamp, nonce, signature);
    }

    private static string Sign(string sessionId, string timestamp, string nonce, string message)
    {
        var signSecret = DeriveSignSecret(PanelEndpoints.PanelChatApiKey);
        var msgHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
        var payload = $"{timestamp}\n{nonce}\n{sessionId}\n{msgHash}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static string DeriveSignSecret(string apiKey)
    {
        using var derive = new HMACSHA256(Encoding.UTF8.GetBytes(DeriveSalt));
        return Convert.ToHexString(derive.ComputeHash(Encoding.UTF8.GetBytes(apiKey))).ToLowerInvariant();
    }
}
