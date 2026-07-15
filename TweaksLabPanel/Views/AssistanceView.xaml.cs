using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Views;

public partial class AssistanceView : UserControl
{
    public AssistanceView()
    {
        InitializeComponent();
        var avatar = new BitmapImage(new Uri(AssetHelper.Avatar("avatar-01.png")));
        UserAvatar.Source = avatar;
        TetsuyaAvatar.Source = new BitmapImage(new Uri(AssetHelper.LogoTetsuya));
    }
}
