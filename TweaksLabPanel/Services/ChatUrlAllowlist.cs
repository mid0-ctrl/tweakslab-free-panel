namespace TweaksLabPanel.Services;

internal static class ChatUrlAllowlist
{
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "tweakslab.fr",
        "www.tweakslab.fr",
        "discord.com",
        "discord.gg",
        "x.com",
        "twitter.com",
        "www.twitter.com",
        "tiktok.com",
        "www.tiktok.com",
        "youtube.com",
        "www.youtube.com",
        "youtu.be",
        "m.youtube.com",
    };

    public static bool IsAllowed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        var host = uri.Host;
        if (AllowedHosts.Contains(host))
            return true;

        foreach (var allowed in AllowedHosts)
        {
            if (host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool TryOpen(string? url)
    {
        if (!IsAllowed(url))
            return false;

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url!.Trim())
        {
            UseShellExecute = true,
        });
        return true;
    }
}
