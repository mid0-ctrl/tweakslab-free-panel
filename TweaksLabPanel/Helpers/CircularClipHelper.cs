using System.Windows;
using System.Windows.Media;

namespace TweaksLabPanel.Helpers;

public static class CircularClipHelper
{
    public static void Attach(FrameworkElement element)
    {
        element.SizeChanged += (_, _) => UpdateClip(element);
        if (element.IsLoaded)
            UpdateClip(element);
        else
            element.Loaded += (_, _) => UpdateClip(element);
    }

    private static void UpdateClip(FrameworkElement element)
    {
        var w = element.ActualWidth;
        var h = element.ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        var cx = w / 2;
        var cy = h / 2;
        var radius = Math.Min(cx, cy);
        element.Clip = new EllipseGeometry(new Point(cx, cy), radius, radius);
    }
}
