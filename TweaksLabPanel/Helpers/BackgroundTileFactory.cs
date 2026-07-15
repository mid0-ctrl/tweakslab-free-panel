using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TweaksLabPanel.Core;

namespace TweaksLabPanel.Helpers;

public static class BackgroundTileFactory
{
    private const double TileCornerRadius = 14;

    public static (Border Tile, Border Check) CreatePresetTile(
        BackgroundCatalog.Preset preset,
        MouseButtonEventHandler onClick,
        FrameworkElement owner,
        double width = 132,
        double height = 104,
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
        };
        preview.Source = LoadBackgroundBitmap(preset.FileName);
        BackgroundMotionHelper.Attach(preview, BackgroundMotionProfile.Preview);

        var check = CreateCheckBadge(checkBrush, iconFont);

        var grid = new Grid
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
                    FontSize = 12,
                    Margin = new Thickness(10),
                    VerticalAlignment = VerticalAlignment.Bottom,
                },
                check,
            },
        };

        if (BackgroundCatalog.IsNew(preset))
        {
            grid.Children.Add(new Border
            {
                Background = checkBrush,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8),
                Child = new TextBlock
                {
                    Text = "NEW",
                    Foreground = Brushes.White,
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                },
            });
        }

        var tile = new Border
        {
            Style = tileStyle,
            Tag = preset.Id,
            Width = stretch ? double.NaN : width,
            Height = height,
            Margin = stretch ? new Thickness(0, 0, 8, 8) : new Thickness(0, 0, 8, 8),
            HorizontalAlignment = stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            ClipToBounds = true,
            Child = grid,
        };
        AttachRoundedClip(tile, TileCornerRadius);
        AttachRoundedClip(grid, TileCornerRadius);
        tile.MouseLeftButtonUp += onClick;
        return (tile, check);
    }

    public static (Border Tile, Border Check) CreateImportTile(
        MouseButtonEventHandler onClick,
        FrameworkElement owner,
        double width = 132,
        double height = 104,
        bool stretch = false)
    {
        var tileStyle = (Style)owner.FindResource("ThemePreviewTile");
        var importBrush = (Brush)owner.FindResource("ImportTileBrush");
        var checkBrush = (Brush)owner.FindResource("PurpleAccentBrush");
        var iconFont = (FontFamily)owner.FindResource("IconFont");
        var textWhite = (Brush)owner.FindResource("TextWhiteBrush");

        var check = CreateCheckBadge(checkBrush, iconFont);

        var grid = new Grid
        {
            ClipToBounds = true,
            Children =
            {
                new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "\uE896",
                            FontFamily = iconFont,
                            FontSize = 20,
                            Foreground = textWhite,
                            HorizontalAlignment = HorizontalAlignment.Center,
                        },
                        new TextBlock
                        {
                            Text = "Importer",
                            Foreground = Brushes.White,
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 12,
                            Margin = new Thickness(0, 6, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Center,
                        },
                    },
                },
                check,
            },
        };

        var tile = new Border
        {
            Style = tileStyle,
            Tag = "custom",
            Width = stretch ? double.NaN : width,
            Height = height,
            Margin = stretch ? new Thickness(0, 0, 8, 8) : new Thickness(0, 0, 8, 8),
            HorizontalAlignment = stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            Background = importBrush,
            Cursor = Cursors.Hand,
            ClipToBounds = true,
            Child = grid,
        };
        AttachRoundedClip(tile, TileCornerRadius);
        AttachRoundedClip(grid, TileCornerRadius);
        tile.MouseLeftButtonUp += onClick;
        return (tile, check);
    }

    public static (Border Card, Border Check, Border Face) CreateWalletCard(
        BackgroundCatalog.Preset preset,
        bool isSelected,
        FrameworkElement owner,
        double width = 164,
        double height = 108)
    {
        var checkBrush = (Brush)owner.FindResource("PurpleAccentBrush");
        var iconFont = (FontFamily)owner.FindResource("IconFont");
        var selectedBorder = (Brush)owner.FindResource("WalletSelectedBorderBrush");
        var idleBorder = (Brush)owner.FindResource("WalletIdleBorderBrush");

        const double corner = 10;

        var preview = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        preview.Source = LoadBackgroundBitmap(preset.FileName);
        BackgroundMotionHelper.Attach(preview, BackgroundMotionProfile.Preview);

        var check = CreateCheckBadge(checkBrush, iconFont);
        check.Margin = new Thickness(6);
        check.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;

        var content = new Grid
        {
            ClipToBounds = true,
            Children =
            {
                preview,
                check,
            },
        };

        AttachRoundedClip(content, corner);

        var slate = new Border
        {
            Tag = preset.Id,
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(corner),
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = isSelected ? selectedBorder : idleBorder,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.Hand,
            SnapsToDevicePixels = true,
            ClipToBounds = true,
            Child = content,
        };

        AttachRoundedClip(slate, corner);

        return (slate, check, slate);
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
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Background = background,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10),
            Child = new TextBlock
            {
                Text = "\uE73E",
                FontFamily = iconFont,
                FontSize = 12,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

    private static BitmapImage? LoadBackgroundBitmap(string file)
    {
        try
        {
            var path = AssetHelper.Background(file);
            if (!File.Exists(path))
                return null;

            return BitmapLoader.LoadPreview(path, 480);
        }
        catch
        {
            return null;
        }
    }
}
