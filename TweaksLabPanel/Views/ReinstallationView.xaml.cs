using System.Windows;
using System.Windows.Controls;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Views;

public partial class ReinstallationView : UserControl
{
    public ReinstallationView() => InitializeComponent();

    private void LaunchInstaller_Click(object sender, RoutedEventArgs e)
    {
        OfferLinks.Open(OfferLinks.Reinstall);
    }
}
