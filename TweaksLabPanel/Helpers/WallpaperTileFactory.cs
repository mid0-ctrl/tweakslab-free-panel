using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Helpers;

public static class WallpaperTileFactory
{
    private const double TileCornerRadius = 12;
    private const double Aspect16x9 = 9.0 / 16.0;

    public static (Border Tile, Border Check) CreatePresetTile(
        WallpaperCatalog.Preset preset,
        MouseButtonEventHandler onClick,
        FrameworkElement owner,
        bool stretch = false)
    {
        var tileStyle = (Style)owner.FindResource("ThemePreviewTile");
        var scrimBrush = (Brush)owner.FindResource("TileScrimBrush");
        var checkBrush = (Brush)owner.FindResource("PurpleAccentBrush");
        var iconFont = (FontFamily)owner.FindResource("IconFont");

        var preview = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Source = WallpaperService.GetPreview(preset.Index),
        };

        var check = CreateCheckBadge(checkBrush, iconFont);

        var content = new Grid
        {
            ClipToBounds = true,
            Children =
            {
                preview,
                new Border { Background = scrimBrush },
                new TextBlock
                {
                    Text = preset.Label,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    Margin = new Thickness(8),
                    VerticalAlignment = VerticalAlignment.Bottom,
                },
                check,
            },
        };

        AttachRoundedClip(content, TileCornerRadius);

        var tile = new Border
        {
            Style = tileStyle,
            Tag = preset.Index,
            Margin = new Thickness(0, 0, 8, 8),
            HorizontalAlignment = stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x10, 0x1A)),
            Cursor = Cursors.Hand,
            ClipToBounds = true,
            Child = content,
        };

        void SyncAspectRatio()
        {
            if (tile.ActualWidth > 0)
                tile.Height = tile.ActualWidth * Aspect16x9;
        }

        tile.SizeChanged += (_, _) => SyncAspectRatio();
        tile.Loaded += (_, _) => SyncAspectRatio();

        AttachRoundedClip(tile, TileCornerRadius);
        tile.MouseLeftButtonUp += onClick;
        return (tile, check);
    }

    private static void AttachRoundedClip(FrameworkElement element, double corner)
    {
        element.SizeChanged += (_, _) =>
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
                return;

            element.Clip = new RectangleGeometry(
                new Rect(0, 0, element.ActualWidth, element.ActualHeight),
                corner,
                corner);
        };
    }

    private static Border CreateCheckBadge(Brush background, FontFamily iconFont) =>
        new()
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = background,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(8),
            Visibility = Visibility.Collapsed,
            Child = new TextBlock
            {
                Text = "\uE73E",
                FontFamily = iconFont,
                FontSize = 11,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
}
