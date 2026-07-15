using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Views;

public partial class JeuxView : UserControl
{
    public JeuxView()
    {
        InitializeComponent();
        FortniteImg.Source = new BitmapImage(new Uri(AssetHelper.Game("fortnite.png")));
        ValorantImg.Source = new BitmapImage(new Uri(AssetHelper.Game("valorant.png")));
        CsgoImg.Source = new BitmapImage(new Uri(AssetHelper.Game("csgo.png")));
    }
}
