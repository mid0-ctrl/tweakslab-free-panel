using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Views;

public partial class ReinstallationView : UserControl
{
    public ReinstallationView() => InitializeComponent();

    private void LaunchInstaller_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(PanelEndpoints.WebsiteUrl) { UseShellExecute = true });
    }
}
