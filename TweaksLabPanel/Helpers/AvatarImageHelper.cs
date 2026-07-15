using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TweaksLabPanel.Helpers;

public static class AvatarImageHelper
{
    public const double GridZoom = 1.24;
    public const double MainZoom = 1.2;
    public const double SidebarZoom = 1.24;

    public static void Prepare(Image image, double zoom)
    {
        image.Stretch = Stretch.UniformToFill;
        image.HorizontalAlignment = HorizontalAlignment.Stretch;
        image.VerticalAlignment = VerticalAlignment.Stretch;
        image.SnapsToDevicePixels = true;
        image.RenderTransformOrigin = new Point(0.5, 0.5);
        image.RenderTransform = new ScaleTransform(zoom, zoom);
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
    }

    public static Grid WrapForCircleClip(Border slot, Image image)
    {
        slot.Child = null;
        var host = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        host.Children.Add(image);
        slot.Child = host;
        CircularClipHelper.Attach(host);
        return host;
    }
}
