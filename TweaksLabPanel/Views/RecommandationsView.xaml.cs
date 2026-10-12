using System.Windows.Controls;
using System.Windows.Input;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Views;

public partial class RecommandationsView : UserControl
{
    public RecommandationsView()
    {
        InitializeComponent();
        RecommendationsOfferLink.Text = LocalizationService.T(
            $"Découvrir l’optimisation complète TweaksLab, {OfferLinks.StartingPrice} →",
            $"Discover the full TweaksLab optimization, {OfferLinks.StartingPrice} →");
    }

    private void RecommendationsOffer_Click(object sender, MouseButtonEventArgs e) =>
        OfferLinks.Open(OfferLinks.Recommendations);
}
