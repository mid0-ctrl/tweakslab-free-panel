using System.Windows.Controls;

namespace TweaksLabPanel.Views;

public partial class PlaceholderView : UserControl
{
    public PlaceholderView(string title)
    {
        InitializeComponent();
        PageTitleText.Text = title;
    }
}
