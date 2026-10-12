using System.Diagnostics;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Helpers;

/// <summary>
/// Tunnel de vente : tous les liens vers tweakslab.fr passent ici, avec un suivi par emplacement
/// (utm_content) pour savoir quel bouton du panel amène les ventes.
/// </summary>
public static class OfferLinks
{
    public const string HomeBanner = "home-banner";
    public const string ScoreDetail = "detail-score";
    public const string Tips = "astuces";
    public const string Cleanup = "nettoyage";
    public const string Gpu = "gpu";
    public const string Chat = "assistance";
    public const string Recommendations = "recommandations";
    public const string Reinstall = "reinstallation";
    public const string SidebarWebsite = "sidebar-site";
    public const string Creator = "programme-createur";

    public static string StartingPrice => LocalizationService.T(
        $"dès {PanelEndpoints.OfferStartingPriceFr}",
        $"from {PanelEndpoints.OfferStartingPriceEn}");

    public static string BuildUrl(string placement) =>
        $"{PanelEndpoints.WebsiteUrl}/?utm_source=free-panel&utm_medium=app&utm_campaign=optimisation" +
        $"&utm_content={Uri.EscapeDataString(placement)}";

    public static void Open(string placement)
    {
        try
        {
            Process.Start(new ProcessStartInfo(BuildUrl(placement)) { UseShellExecute = true });
        }
        catch
        {
            // Navigateur indisponible : rien à faire.
        }
    }
}
