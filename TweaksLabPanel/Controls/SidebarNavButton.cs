using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TweaksLabPanel.Controls;

public sealed class SidebarNavButton
{
    public Border Root { get; }
    public string PageKey { get; }

    private readonly Border _accentBar;
    private readonly TextBlock _icon;
    private readonly TextBlock _label;
    private readonly bool _goldLabel;
    private bool _active;

    private static Brush TextWhite => (Brush)Application.Current.Resources["TextWhiteBrush"]!;
    private static Brush TextGray => (Brush)Application.Current.Resources["TextGrayBrush"]!;
    private static Brush GoldLabelBrush => (Brush)Application.Current.Resources["GoldLabelGradientBrush"]!;
    private static FontFamily IconFont => (FontFamily)Application.Current.Resources["IconFont"]!;
    private static FontFamily AppFont => (FontFamily)Application.Current.Resources["AppFont"]!;

    private static readonly SolidColorBrush HoverBg = new(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush ActiveBg = new(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush ActiveAccent = new(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush GoldHoverBg = new(Color.FromArgb(0x18, 0xFB, 0xBF, 0x24));

    static SidebarNavButton()
    {
        HoverBg.Freeze();
        ActiveBg.Freeze();
        ActiveAccent.Freeze();
        GoldHoverBg.Freeze();
    }

    private const double ItemHeight = 32;
    private const double IconColumnWidth = 22;
    private const double IconFontSize = 14;
    private const double LabelFontSize = 13;

    public SidebarNavButton(string pageKey, string label, string glyph, bool goldLabel = false)
    {
        PageKey = pageKey;
        _goldLabel = goldLabel;

        _accentBar = new Border
        {
            Width = 3,
            CornerRadius = new CornerRadius(2),
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 8, 6, 8),
            VerticalAlignment = VerticalAlignment.Center,
        };

        _icon = new TextBlock
        {
            Text = glyph,
            FontFamily = IconFont,
            Foreground = goldLabel ? GoldLabelBrush : TextGray,
            FontSize = IconFontSize,
            Width = IconColumnWidth,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0, 1, 0, 0),
        };

        _label = new TextBlock
        {
            Text = label,
            Foreground = goldLabel ? GoldLabelBrush : TextGray,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = AppFont,
            FontSize = LabelFontSize,
            FontWeight = FontWeights.Normal,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 1, 4, 0),
        };

        var content = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(IconColumnWidth) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_icon, 0);
        Grid.SetColumn(_label, 1);
        content.Children.Add(_icon);
        content.Children.Add(_label);

        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = true,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_accentBar, 0);
        Grid.SetColumn(content, 1);
        grid.Children.Add(_accentBar);
        grid.Children.Add(content);

        Root = new Border
        {
            Height = ItemHeight,
            CornerRadius = new CornerRadius(12),
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 1, 0, 5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = true,
            Child = grid,
            Focusable = false,
        };

        Root.MouseEnter += (_, _) =>
        {
            if (_active) return;

            Root.Background = _goldLabel ? GoldHoverBg : HoverBg;
            if (_goldLabel)
            {
                _label.Foreground = GoldLabelBrush;
                _icon.Foreground = GoldLabelBrush;
            }
            else
            {
                _label.Foreground = TextWhite;
            }
        };

        Root.MouseLeave += (_, _) =>
        {
            if (_active) return;

            Root.Background = Brushes.Transparent;
            if (_goldLabel)
            {
                _label.Foreground = GoldLabelBrush;
                _icon.Foreground = GoldLabelBrush;
            }
            else
            {
                _label.Foreground = TextGray;
            }
        };
    }

    public void SetActive(bool active)
    {
        if (_goldLabel)
            return;

        _active = active;
        _accentBar.Background = active ? ActiveAccent : Brushes.Transparent;
        _icon.Foreground = active ? TextWhite : TextGray;
        _label.Foreground = active ? TextWhite : TextGray;
        _label.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        Root.Background = active ? ActiveBg : Brushes.Transparent;
    }
}
