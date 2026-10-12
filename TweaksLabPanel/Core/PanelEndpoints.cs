// URLs réseau autorisées du Free Panel — seul point central (pas d'URL en dur dans les .xaml).
// Règle n°1 : aucun code importé depuis un autre repo TweaksLab ; ces constantes sont des appels HTTP, pas du code copié.
namespace TweaksLabPanel.Core;

public static class PanelEndpoints
{
    public const string UpdatesBaseUrl = "https://tweakslab-free-panel-updates.onrender.com";
    public const string GitHubLatestReleaseApi =
        "https://api.github.com/repos/mid0-ctrl/tweakslab-free-panel/releases/latest";
    public const string ChatBaseUrl = "https://web-production-8a75c4.up.railway.app";
    public const string ChatPanelPath = "/panel";
    /// <summary>Optionnel — doit correspondre à PANEL_API_KEY sur Railway (filtre spam basique).</summary>
    public const string PanelChatApiKey = "53cdcf9d02bba08fad7eae6de33eae261c61dc070a9034036af0a236d913c0be";
    public const string WebsiteUrl = "https://tweakslab.fr";

    /// <summary>Prix d'entrée de l'optimisation complète affiché dans le panel (un seul endroit à modifier).</summary>
    public const string OfferStartingPriceFr = "24,99 €";
    public const string OfferStartingPriceEn = "€24.99";

    public const string SocialX = "https://x.com/TweaksLab_";
    public const string SocialTikTok = "https://www.tiktok.com/@tweakslab";
    public const string SocialYoutube = "https://www.youtube.com/@TweaksLab";
    public const string SocialDiscord = "https://discord.com/invite/tweakslab";

    /// <summary>Lien tutoriel DDU + réinstallation pilotes NVIDIA (à renseigner).</summary>
    public const string GpuNvidiaDduVideoUrl = "";

    /// <summary>Lien tutoriel DDU + réinstallation pilotes AMD (à renseigner).</summary>
    public const string GpuAmdDduVideoUrl = "";

    /// <summary>Tutoriel TikTok — activer XMP / EXPO dans le BIOS.</summary>
    public const string XmpTutorialUrl = "https://www.tiktok.com/@tweakslab/video/7631669028305636630?lang=fr";
}
