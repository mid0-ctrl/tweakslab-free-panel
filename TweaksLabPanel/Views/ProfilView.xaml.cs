using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Views;

public partial class ProfilView : UserControl
{
    private readonly List<(Grid Slot, Grid ClipHost, Image Image, Ellipse Ring)> _avatarSlots = [];
    private readonly Dictionary<string, (Border Tile, Border Check)> _gameTiles = new();
    private readonly Dictionary<int, (Border Tile, Border Check)> _wallpaperTiles = new();
    private readonly HashSet<string> _favoriteGames = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7));
    private static readonly Brush AvatarRingBrush = Brushes.Black;
    private static readonly Brush IdleTileBorder = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

    private string _displayName = string.Empty;
    private bool _editingName;
    private bool _walletBuilt;
    private bool _walletScrollAdjusting;
    private double _walletLoopWidth;

    private const double WalletCardWidth = 112;
    private const double WalletCardHeight = 72;
    private const double WalletCardGap = 10;
    private const int WalletLoopCopies = 3;

    private readonly List<WalletCardEntry> _walletCards = [];

    private static readonly IEasingFunction WalletEase = new CubicEase { EasingMode = EasingMode.EaseOut };
    private static readonly TimeSpan WalletHoverDuration = TimeSpan.FromMilliseconds(260);

    private sealed class WalletCardEntry
    {
        public required string Id { get; init; }
        public required Border Card { get; init; }
        public required Border Check { get; init; }
        public required Border Face { get; init; }
        public required ScaleTransform Scale { get; init; }
        public required TranslateTransform Lift { get; init; }
        public double BaseScale { get; set; } = 1;
        public double BaseLift { get; set; }
    }

    public ProfilView()
    {
        InitializeComponent();
        AccentBrush.Freeze();
        IdleTileBorder.Freeze();

        SetDisplayName(UserProfileStore.Instance.ResolvedDisplayName);

        LoadImage(Avatar1, "avatar-01.png");
        LoadImage(Avatar2, "avatar-02.png");
        LoadImage(Avatar3, "avatar-03.png");
        LoadImage(Avatar4, "avatar-04.png");
        LoadImage(Avatar5, "avatar-05.png");
        LoadImage(Avatar6, "avatar-06.png");
        LoadImage(Avatar7, "avatar-07.png");
        LoadImage(Avatar8, "avatar-08.png");
        LoadImage(Avatar9, "avatar-09.png");

        _avatarSlots.AddRange(
        [
            (AvSlot1, AvSlot1Clip, Avatar1, AvSlot1Ring),
            (AvSlot2, AvSlot2Clip, Avatar2, AvSlot2Ring),
            (AvSlot3, AvSlot3Clip, Avatar3, AvSlot3Ring),
            (AvSlot4, AvSlot4Clip, Avatar4, AvSlot4Ring),
            (AvSlot5, AvSlot5Clip, Avatar5, AvSlot5Ring),
            (AvSlot6, AvSlot6Clip, Avatar6, AvSlot6Ring),
            (AvSlot7, AvSlot7Clip, Avatar7, AvSlot7Ring),
            (AvSlot8, AvSlot8Clip, Avatar8, AvSlot8Ring),
            (AvSlot9, AvSlot9Clip, Avatar9, AvSlot9Ring),
        ]);

        foreach (var (_, clipHost, image, _) in _avatarSlots)
        {
            AvatarImageHelper.Prepare(image, AvatarImageHelper.GridZoom);
            CircularClipHelper.Attach(clipHost);
        }

        AvatarImageHelper.Prepare(MainAvatarImage, AvatarImageHelper.MainZoom);
        CircularClipHelper.Attach(MainAvatarClipHost);

        GameFortnite.Source = LoadBitmap(AssetHelper.Game("fortnite.png"));
        GameValorant.Source = LoadBitmap(AssetHelper.Game("valorant.png"));
        GameCsgo.Source = LoadBitmap(AssetHelper.Game("csgo.png"));

        _gameTiles["fortnite"] = (GameFortniteTile, GameFortniteCheck);
        _gameTiles["valorant"] = (GameValorantTile, GameValorantCheck);
        _gameTiles["csgo"] = (GameCsgoTile, GameCsgoCheck);
        foreach (var game in UserProfileStore.Instance.FavoriteGames)
            _favoriteGames.Add(game);
        RefreshFavoriteGamesSelection();

        InitBackgroundWallet();
        LoadWallpaperPreviews();
        RefreshName();
        BioBox.Text = UserProfileStore.Instance.Bio;
        RestoreSavedAvatar();
        MemberSinceText.Text = FormatMemberSince(AppSettingsStore.Instance.FirstLaunchDate);
    }

    private static string FormatMemberSince(DateOnly date)
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo(LocalizationService.IsEnglish ? "en-US" : "fr-FR");
        return LocalizationService.IsEnglish
            ? $"Member since {date.ToString("MMMM d, yyyy", culture)}"
            : $"Membre depuis le {date.ToString("d MMMM yyyy", culture)}";
    }

    public void RefreshBackgroundWallet()
    {
        if (!_walletBuilt)
            InitBackgroundWallet();
        else
            RefreshWalletSelection();
    }

    private void InitBackgroundWallet()
    {
        BackgroundWallet.Children.Clear();
        _walletCards.Clear();
        BackgroundWalletCount.Text = LocalizationService.T($"{BackgroundCatalog.All.Length} fonds dans la bibliothèque", $"{BackgroundCatalog.All.Length} backgrounds in the library");

        var selectedId = BackgroundSelectionState.IsCustom
            ? BackgroundCatalog.DefaultId
            : BackgroundSelectionState.SelectedId;

        var presets = BackgroundCatalog.All;
        _walletLoopWidth = presets.Length * (WalletCardWidth + WalletCardGap);

        for (var copy = 0; copy < WalletLoopCopies; copy++)
        {
            for (var i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];
                var isSelected = preset.Id == selectedId && !BackgroundSelectionState.IsCustom;
                AddWalletCard(preset, isSelected);
            }
        }

        BackgroundWalletScroll.ScrollChanged -= BackgroundWalletScroll_Changed;
        BackgroundWalletScroll.ScrollChanged += BackgroundWalletScroll_Changed;

        _walletBuilt = true;
        RefreshWalletSelection();
        Dispatcher.BeginInvoke(ResetWalletScrollToMiddle, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void AddWalletCard(BackgroundCatalog.Preset preset, bool isSelected)
    {
        var (card, check, face) = BackgroundTileFactory.CreateWalletCard(
            preset, isSelected, this, WalletCardWidth, WalletCardHeight);

        var scale = new ScaleTransform(1, 1);
        var lift = new TranslateTransform(0, 0);
        card.RenderTransform = new TransformGroup
        {
            Children = { scale, lift },
        };
        card.RenderTransformOrigin = new Point(0.5, 0.5);
        card.Margin = new Thickness(0, 0, WalletCardGap, 0);
        card.MouseLeftButtonUp += WalletPreset_Click;

        var entry = new WalletCardEntry
        {
            Id = preset.Id,
            Card = card,
            Check = check,
            Face = face,
            Scale = scale,
            Lift = lift,
        };

        AttachWalletCardHover(entry);
        _walletCards.Add(entry);
        BackgroundWallet.Children.Add(card);
    }

    private void ResetWalletScrollToMiddle()
    {
        if (_walletLoopWidth <= 0)
            return;

        _walletScrollAdjusting = true;
        BackgroundWalletScroll.ScrollToHorizontalOffset(_walletLoopWidth);
        _walletScrollAdjusting = false;
        ScrollWalletToSelected();
    }

    private void BackgroundWalletScroll_Changed(object sender, ScrollChangedEventArgs e)
    {
        if (_walletScrollAdjusting || _walletLoopWidth <= 0)
            return;

        var offset = BackgroundWalletScroll.HorizontalOffset;
        const double tolerance = 1;

        if (offset >= _walletLoopWidth * 2 - tolerance)
        {
            _walletScrollAdjusting = true;
            BackgroundWalletScroll.ScrollToHorizontalOffset(offset - _walletLoopWidth);
            _walletScrollAdjusting = false;
        }
        else if (offset <= tolerance)
        {
            _walletScrollAdjusting = true;
            BackgroundWalletScroll.ScrollToHorizontalOffset(offset + _walletLoopWidth);
            _walletScrollAdjusting = false;
        }
    }

    private void ScrollWalletToSelected()
    {
        var selectedId = BackgroundSelectionState.IsCustom
            ? null
            : BackgroundSelectionState.SelectedId;

        if (selectedId is null || _walletLoopWidth <= 0)
            return;

        var index = Array.FindIndex(BackgroundCatalog.All, p => p.Id == selectedId);
        if (index < 0)
            return;

        var target = _walletLoopWidth + index * (WalletCardWidth + WalletCardGap);

        _walletScrollAdjusting = true;
        BackgroundWalletScroll.ScrollToHorizontalOffset(target);
        _walletScrollAdjusting = false;
    }

    private void BackgroundWalletScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scroll)
            return;

        scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void RefreshWalletSelection()
    {
        var selectedId = BackgroundSelectionState.IsCustom
            ? null
            : BackgroundSelectionState.SelectedId;

        var faint = (Brush)FindResource("WalletIdleBorderBrush");
        var selectedBorder = (Brush)FindResource("WalletSelectedBorderBrush");

        foreach (var entry in _walletCards)
        {
            var selected = entry.Id == selectedId;
            entry.Check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            entry.Face.BorderBrush = selected ? selectedBorder : faint;
            entry.Face.BorderThickness = new Thickness(1);
            entry.Card.Opacity = selected ? 1 : 0.78;

            entry.BaseScale = selected ? 1.03 : 1;
            entry.BaseLift = 0;
            AnimateWalletScale(entry.Scale, entry.BaseScale);
            AnimateWalletLift(entry.Lift, entry.BaseLift);
            Panel.SetZIndex(entry.Card, selected ? 10 : 0);
        }
    }

    private void AttachWalletCardHover(WalletCardEntry entry)
    {
        const double hoverScaleBoost = 1.06;
        const double hoverLift = 0;

        entry.Card.MouseEnter += (_, _) =>
        {
            Panel.SetZIndex(entry.Card, 20);
            entry.Card.Opacity = 1;
            AnimateWalletScale(entry.Scale, entry.BaseScale * hoverScaleBoost);
            AnimateWalletLift(entry.Lift, hoverLift);
        };

        entry.Card.MouseLeave += (_, _) => RefreshWalletSelection();
    }

    private static void AnimateWalletLift(TranslateTransform transform, double targetY)
    {
        transform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(targetY, WalletHoverDuration) { EasingFunction = WalletEase });
    }

    private static void AnimateWalletScale(ScaleTransform transform, double target)
    {
        transform.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(target, WalletHoverDuration) { EasingFunction = WalletEase });
        transform.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(target, WalletHoverDuration) { EasingFunction = WalletEase });
    }

    private void WalletPreset_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id })
            return;

        var preset = BackgroundCatalog.All.FirstOrDefault(p => p.Id == id);
        if (preset is null)
            return;

        if (Window.GetWindow(this) is not MainWindow main)
            return;

        BackgroundSelectionState.SelectedId = id;
        BackgroundSelectionState.IsCustom = false;
        main.SetAppBackground(preset.FileName);
        main.OnBackgroundChanged(id);
        RefreshWalletSelection();
        ScrollWalletToSelected();
        e.Handled = true;
    }

    private void EditName_Click(object sender, MouseButtonEventArgs e)
    {
        if (_editingName) return;
        EnterEditNameMode();
        e.Handled = true;
    }

    private void EnterEditNameMode()
    {
        _editingName = true;
        PseudoBox.Text = _displayName;
        NameText.Visibility = Visibility.Collapsed;
        EditNameIcon.Visibility = Visibility.Collapsed;
        PseudoBox.Visibility = Visibility.Visible;
        PseudoBox.Focus();
        PseudoBox.SelectAll();
    }

    private void ExitEditNameMode(bool commit = true)
    {
        if (!_editingName) return;
        _editingName = false;

        if (commit)
        {
            var entered = PseudoBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(entered))
            {
                UserProfileStore.Instance.SaveDisplayName(entered);
                _displayName = entered;

                if (Window.GetWindow(this) is MainWindow main)
                    main.ApplyDisplayName(entered);
            }
        }

        PseudoBox.Visibility = Visibility.Collapsed;
        NameText.Visibility = Visibility.Visible;
        EditNameIcon.Visibility = Visibility.Visible;
        RefreshName();
    }

    private void PseudoBox_LostFocus(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(() => ExitEditNameMode());

    private void PseudoBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExitEditNameMode();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ExitEditNameMode(commit: false);
            e.Handled = true;
        }
    }

    public void SetDisplayName(string displayName)
    {
        _displayName = string.IsNullOrWhiteSpace(displayName)
            ? UserProfileStore.DefaultDisplayName
            : displayName.Trim();
        PseudoBox.Text = UserProfileStore.Instance.HasDisplayName ? UserProfileStore.Instance.DisplayName : string.Empty;
        RefreshName();
    }

    private void RefreshName()
    {
        NameText.Text = _displayName;
        AvatarInitial.Text = string.IsNullOrEmpty(_displayName)
            ? "?"
            : char.ToUpperInvariant(_displayName[0]).ToString();
    }

    public void ScrollToPersonalization() => PersonalizationSection?.BringIntoView();

    private void OpenBackgroundLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow main)
            main.NavigateToBackgroundLibrary();
    }

    private void OpenWallpaperLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow main)
            main.NavigateToWallpaperLibrary();
    }

    private void LoadWallpaperPreviews()
    {
        WallpaperGrid.Children.Clear();
        _wallpaperTiles.Clear();
        WallpaperCountText.Text = LocalizationService.T($"{WallpaperCatalog.All.Length} fonds dans la bibliothèque", $"{WallpaperCatalog.All.Length} wallpapers in the library");

        foreach (var preset in WallpaperCatalog.Featured)
        {
            var (tile, check) = WallpaperTileFactory.CreatePresetTile(
                preset, Wallpaper_Click, this, stretch: true);
            tile.SizeChanged += WallTile_SizeChanged;
            WallpaperGrid.Children.Add(tile);
            _wallpaperTiles[preset.Index] = (tile, check);
        }

        RefreshWallpaperSelection();
    }

    private void RefreshWallpaperSelection()
    {
        var selected = WallpaperSelectionState.LastAppliedIndex;
        foreach (var (index, (tile, check)) in _wallpaperTiles)
        {
            var isSelected = selected == index;
            tile.BorderBrush = isSelected ? AccentBrush : IdleTileBorder;
            tile.BorderThickness = new Thickness(isSelected ? 2 : 1);
            check.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void AvatarSlot_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Grid slot)
            SelectAvatarSlot(slot);
    }

    private void SelectAvatarSlot(Grid selected)
    {
        ClearAvatarSlotSelection();

        foreach (var (slot, _, _, ring) in _avatarSlots)
        {
            if (slot != selected)
                continue;

            ring.Stroke = AvatarRingBrush;
            ring.StrokeThickness = 2.5;

            if (slot.Tag is string fileName)
                ApplySelectedAvatar(fileName);
        }
    }

    private void ApplySelectedAvatar(string fileName)
    {
        var bitmap = LoadBitmap(AssetHelper.Avatar(fileName));
        if (bitmap is not null)
            ApplyAvatarBitmap(bitmap, fileName);

        var avatarId = System.IO.Path.GetFileNameWithoutExtension(fileName);
        UserProfileStore.Instance.SaveAvatar(avatarId);
    }

    private void ImportAvatar_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = LocalizationService.T("Importer ma photo de profil", "Import my profile picture"),
            Filter = "Images (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp",
            CheckFileExists = true
        };

        if (dlg.ShowDialog() != true)
            return;

        ClearAvatarSlotSelection();
        var bitmap = LoadBitmap(dlg.FileName);
        if (bitmap is not null)
        {
            ApplyAvatarBitmap(bitmap);
            UserProfileStore.Instance.SaveAvatar("custom", dlg.FileName);
        }
    }

    private void BioBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (Window.GetWindow(this) is MainWindow main)
            main.OnBioChanged(BioBox.Text);
    }

    private void RestoreSavedAvatar()
    {
        var profile = UserProfileStore.Instance;
        if (!string.IsNullOrWhiteSpace(profile.AvatarCustomPath) && File.Exists(profile.AvatarCustomPath))
        {
            var bitmap = LoadBitmap(profile.AvatarCustomPath);
            if (bitmap is not null)
                ApplyAvatarBitmap(bitmap, null);
            return;
        }

        var fileName = profile.AvatarId.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? profile.AvatarId
            : $"{profile.AvatarId}.png";

        foreach (var (slot, _, _, _) in _avatarSlots)
        {
            if (slot.Tag is string tag && string.Equals(tag, fileName, StringComparison.OrdinalIgnoreCase))
            {
                SelectAvatarSlot(slot);
                return;
            }
        }

        ApplySelectedAvatar(fileName);
    }

    private void ClearAvatarSlotSelection()
    {
        foreach (var (_, _, _, ring) in _avatarSlots)
            ring.StrokeThickness = 0;
    }

    private void ApplyAvatarBitmap(BitmapSource bitmap, string? assetFileName = null)
    {
        MainAvatarImage.Source = bitmap;
        MainAvatarImage.Visibility = Visibility.Visible;
        AvatarInitial.Visibility = Visibility.Collapsed;

        if (Window.GetWindow(this) is not MainWindow main)
            return;

        if (assetFileName is not null)
            main.SetSidebarAvatar(assetFileName);
        else
            main.SetSidebarAvatarImage(bitmap);
    }

    private void FavoriteGame_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id } || !_gameTiles.ContainsKey(id))
            return;

        if (_favoriteGames.Contains(id))
            _favoriteGames.Remove(id);
        else
            _favoriteGames.Add(id);

        UserProfileStore.Instance.SaveFavoriteGames(_favoriteGames);
        RefreshFavoriteGamesSelection();
        e.Handled = true;
    }

    private void RefreshFavoriteGamesSelection()
    {
        foreach (var (id, (tile, check)) in _gameTiles)
        {
            var selected = _favoriteGames.Contains(id);
            tile.BorderBrush = selected ? AccentBrush : IdleTileBorder;
            tile.BorderThickness = new Thickness(selected ? 2 : 1);
            check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void Wallpaper_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: int index })
            return;

        if (!WallpaperService.Apply(index))
        {
            MessageBox.Show(LocalizationService.T("Impossible d'appliquer ce fond d'écran.", "Couldn't apply this wallpaper."), LocalizationService.T("Fonds d'écran", "Wallpapers"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RefreshWallpaperSelection();
        e.Handled = true;
    }

    private void WallTile_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Border border)
            border.Clip = new RectangleGeometry(
                new Rect(0, 0, border.ActualWidth, border.ActualHeight), 18, 18);
    }

    private static void LoadImage(Image img, string file)
    {
        var bitmap = LoadBitmap(AssetHelper.Avatar(file));
        if (bitmap is not null)
            img.Source = bitmap;
    }

    private static BitmapImage? LoadBitmap(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public FrameworkElement? GetOnboardingTarget(int step) => step switch
    {
        0 => OnboardingProfileIdentity,
        _ => null,
    };

    public void ScrollToTop() => PageScroll.ScrollToVerticalOffset(0);
}
