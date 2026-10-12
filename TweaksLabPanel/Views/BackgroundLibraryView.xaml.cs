using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Views;

public partial class BackgroundLibraryView : UserControl
{
    private readonly Dictionary<string, (Border Tile, Border Check, string File)> _tiles = new();
    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7));
    private static readonly Brush IdleTileBorder = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

    static BackgroundLibraryView()
    {
        AccentBrush.Freeze();
        IdleTileBorder.Freeze();
    }

    public BackgroundLibraryView()
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

    private void BuildLibrary()
    {
        BackgroundLibraryGrid.Children.Clear();
        _tiles.Clear();

        foreach (var preset in BackgroundCatalog.All)
        {
            var (tile, check) = BackgroundTileFactory.CreatePresetTile(
                preset, Background_Click, this, stretch: true);
            BackgroundLibraryGrid.Children.Add(tile);
            _tiles[preset.Id] = (tile, check, preset.FileName);
        }

        var (importTile, importCheck) = BackgroundTileFactory.CreateImportTile(
            ImportBackground_Click, this, stretch: true);
        BackgroundLibraryGrid.Children.Add(importTile);
        _tiles["custom"] = (importTile, importCheck, "");
    }

    private void Background_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id } || !_tiles.ContainsKey(id))
            return;
        if (id == BackgroundSelectionState.SelectedId && !BackgroundSelectionState.IsCustom)
            return;

        ApplySelection(id);
        e.Handled = true;
    }

    private void ImportBackground_Click(object sender, MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = LocalizationService.T("Importer un fond", "Import a background"),
            Filter = "Images (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp",
            CheckFileExists = true,
        };

        if (dlg.ShowDialog() != true)
            return;

        if (Window.GetWindow(this) is MainWindow main)
        {
            main.SetAppBackgroundFromFile(dlg.FileName);
            main.OnBackgroundChanged("custom", dlg.FileName);
        }

        BackgroundSelectionState.SelectedId = "custom";
        BackgroundSelectionState.IsCustom = true;
        RefreshSelectionVisuals();
        e.Handled = true;
    }

    private void ApplySelection(string id)
    {
        if (Window.GetWindow(this) is not MainWindow main)
            return;

        BackgroundSelectionState.SelectedId = id;
        BackgroundSelectionState.IsCustom = false;
        main.SetAppBackground(_tiles[id].File);
        main.OnBackgroundChanged(id);
        RefreshSelectionVisuals();
    }

    private void RefreshSelectionVisuals()
    {
        var selectedId = BackgroundSelectionState.IsCustom
            ? "custom"
            : BackgroundSelectionState.SelectedId;

        foreach (var (id, (tile, check, _)) in _tiles)
        {
            var selected = id == selectedId;
            tile.BorderBrush = selected ? AccentBrush : IdleTileBorder;
            tile.BorderThickness = new Thickness(selected ? 2 : 1);
            check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow main)
            main.NavigateTo(main.BackgroundLibraryReturnPage);
    }
}
