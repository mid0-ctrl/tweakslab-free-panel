using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Views;

public partial class WallpaperLibraryView : UserControl
{
    private readonly Dictionary<int, (Border Tile, Border Check)> _tiles = new();
    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7));
    private static readonly Brush IdleTileBorder = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

    static WallpaperLibraryView()
    {
        AccentBrush.Freeze();
        IdleTileBorder.Freeze();
    }

    public WallpaperLibraryView()
    {
        InitializeComponent();
        BuildLibrary();
        RefreshSelection();
    }

    public void RefreshSelection() => RefreshSelectionVisuals();

    public void ConfigureReturn(string returnTo)
    {
        BackButtonText.Text = returnTo == "Accueil"
            ? LocalizationService.T("Retour à l'accueil", "Back to home")
            : LocalizationService.T("Retour au profil", "Back to profile");
    }

    public void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(() =>
        {
            PageScroll.ScrollToEnd();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void BuildLibrary()
    {
        CollectionGrid.Children.Clear();
        _tiles.Clear();

        foreach (var preset in WallpaperCatalog.All)
            AddTile(CollectionGrid, preset);
    }

    private void AddTile(Panel host, WallpaperCatalog.Preset preset)
    {
        var (tile, check) = WallpaperTileFactory.CreatePresetTile(
            preset, Wallpaper_Click, this, stretch: true);
        host.Children.Add(tile);
        _tiles[preset.Index] = (tile, check);
    }

    private void Wallpaper_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: int index })
            return;

        if (!WallpaperService.Apply(index))
        {
            MessageBox.Show(
                LocalizationService.T("Impossible d'appliquer ce fond d'écran.", "Couldn't apply this wallpaper."),
                LocalizationService.T("Fonds d'écran", "Wallpapers"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        WallpaperSelectionState.LastAppliedIndex = index;
        RefreshSelectionVisuals();
        e.Handled = true;
    }

    private void RefreshSelectionVisuals()
    {
        var selected = WallpaperSelectionState.LastAppliedIndex;
        foreach (var (index, (tile, check)) in _tiles)
        {
            var isSelected = selected == index;
            tile.BorderBrush = isSelected ? AccentBrush : IdleTileBorder;
            tile.BorderThickness = new Thickness(isSelected ? 2 : 1);
            check.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow main)
            main.NavigateTo(main.WallpaperLibraryReturnPage);
    }
}
