using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TweaksLabPanel.Controls;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;
using TweaksLabPanel.Views;

namespace TweaksLabPanel;

public partial class MainWindow : Window
{
    private static bool ShouldShowOnboarding => !AppSettingsStore.Instance.OnboardingCompleted;

    private static void CompleteOnboardingIfNeeded() =>
        AppSettingsStore.Instance.MarkOnboardingCompleted();

    private readonly Dictionary<string, UserControl> _pages = new();
    private readonly Dictionary<string, SidebarNavButton> _navButtons = new();
    private bool _notifAnimating;
    private bool _notifOpen;
    private string? _escapeBackPage;
    private string _currentPage = "Accueil";
    private string _wallpaperLibraryReturnPage = "Profil";
    private string _backgroundLibraryReturnPage = "Profil";

    public string WallpaperLibraryReturnPage => _wallpaperLibraryReturnPage;
    public string BackgroundLibraryReturnPage => _backgroundLibraryReturnPage;

    private static readonly IEasingFunction NotifEaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };
    private static readonly IEasingFunction NotifEaseIn = new CubicEase { EasingMode = EasingMode.EaseIn };
    private static readonly IEasingFunction NotifPopEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.2 };
    private static readonly BlurEffect PseudoBackdropBlur = new() { Radius = 18, RenderingBias = RenderingBias.Quality };

    static MainWindow()
    {
        PseudoBackdropBlur.Freeze();
    }

    public MainWindow()
    {
        InitializeComponent();
        ApplyWindowIcon();
        BuildSidebar();
        BuildSocialBar();
        LoadAssets();
        Loaded += (_, _) =>
        {
            AvatarImageHelper.Prepare(SidebarAvatar, AvatarImageHelper.SidebarZoom);
            CircularClipHelper.Attach(SidebarAvatarHost);
            ApplyRoundedWindowClip();
            ShowPseudoOverlayIfNeeded();
            QueueStartupOnboarding();
        };
        NavigateTo("Accueil");
        ApplyDisplayName(UserProfileStore.Instance.ResolvedDisplayName);
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged() =>
        Dispatcher.BeginInvoke(ApplyLanguage, DispatcherPriority.Background);

    /// <summary>
    /// Changement de langue : menu reconstruit et pages recréées (leurs textes générés en code suivent).
    /// Une conversation en cours avec Bryan est conservée.
    /// </summary>
    private void ApplyLanguage()
    {
        BuildSidebar();
        BuildSocialBar();

        var kept = _pages
            .Where(p => p.Value is AssistanceView chat && chat.HasConversation)
            .ToList();
        _pages.Clear();
        foreach (var (key, page) in kept)
            _pages[key] = page;

        switch (_currentPage)
        {
            case "BibliothequeFondsEcran":
                NavigateToWallpaperLibrary(_wallpaperLibraryReturnPage);
                break;
            case "BibliothequeFonds":
                NavigateToBackgroundLibrary(_backgroundLibraryReturnPage);
                break;
            default:
                NavigateTo(_currentPage);
                break;
        }

        ApplyDisplayName(UserProfileStore.Instance.ResolvedDisplayName);
    }

    private void ApplyWindowIcon()
    {
        var pngPath = AssetHelper.Logo;
        if (File.Exists(pngPath))
        {
            using var pngStream = File.OpenRead(pngPath);
            Icon = BitmapFrame.Create(
                pngStream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            return;
        }

        var iconPath = AssetHelper.LogoIco;
        if (!File.Exists(iconPath))
            return;

        using var stream = File.OpenRead(iconPath);
        Icon = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
    }

    private void ShowPseudoOverlayIfNeeded()
    {
        if (UserProfileStore.Instance.HasDisplayName)
        {
            PseudoOverlay.Visibility = Visibility.Collapsed;
            AppShell.Effect = null;
            return;
        }

        AppShell.Effect = PseudoBackdropBlur;
        PseudoOverlay.Visibility = Visibility.Visible;
        PseudoInput.Text = string.Empty;
        PseudoContinueBtn.IsEnabled = false;
        PseudoInput.Focus();
    }

    private void PseudoInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        PseudoContinueBtn.IsEnabled = !string.IsNullOrWhiteSpace(PseudoInput.Text);
    }

    private void PseudoInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && PseudoContinueBtn.IsEnabled)
        {
            CompletePseudoSetup();
            e.Handled = true;
        }
    }

    private void PseudoContinue_Click(object sender, RoutedEventArgs e) => CompletePseudoSetup();

    private void CompletePseudoSetup()
    {
        var pseudo = PseudoInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(pseudo))
            return;

        UserProfileStore.Instance.SaveDisplayName(pseudo);
        AppShell.Effect = null;
        PseudoOverlay.Visibility = Visibility.Collapsed;
        ApplyDisplayName(pseudo);
        ShowOnboardingIfNeeded();
    }

    public void ApplyDisplayName(string displayName)
    {
        var shown = string.IsNullOrWhiteSpace(displayName)
            ? UserProfileStore.DefaultDisplayName
            : displayName.Trim();

        SidebarPseudo.Text = shown;
        SidebarBio.Text = string.IsNullOrWhiteSpace(UserProfileStore.Instance.Bio)
            ? string.Empty
            : $"\u00AB {UserProfileStore.Instance.Bio.Trim()} \u00BB";
        SidebarBio.Visibility = string.IsNullOrWhiteSpace(SidebarBio.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;

        foreach (var page in _pages.Values)
        {
            switch (page)
            {
                case HomeView home:
                    home.SetDisplayName(shown);
                    break;
                case ProfilView profil:
                    profil.SetDisplayName(shown);
                    break;
            }
        }
    }

    private void RootContent_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyRoundedWindowClip();

    private void ApplyRoundedWindowClip()
    {
        if (RootContent.ActualWidth <= 0 || RootContent.ActualHeight <= 0)
            return;

        const double radius = 32;
        RootContent.Clip = new RectangleGeometry(
            new Rect(0, 0, RootContent.ActualWidth, RootContent.ActualHeight),
            radius, radius);
    }

    private void BuildSidebar()
    {
        NavPanel.Children.Clear();
        _navButtons.Clear();

        AddNav("Accueil", LocalizationService.NavHome, "\uE80F");
        AddNav("Assistance", LocalizationService.NavAssistance, "\uE8F2");
        AddNav("MonPc", LocalizationService.NavMonPc, "\uE977");
        AddDivider();

        AddNav("Jeux", LocalizationService.NavJeux, "\uE7FC");
        AddNav("Application", LocalizationService.NavApplication, "\uE71D");
        AddDivider();

        AddNav("Nettoyer", LocalizationService.NavNettoyer, "\uE74D");
        AddNav("Reinstallation", LocalizationService.NavReinstallation, "\uE895");
        AddDivider();

        AddNav("Recommandations", LocalizationService.NavRecommandations, "\uE734");
        AddNav("Astuces", LocalizationService.NavAstuces, "\uE82F");
        AddNav("Gpu", LocalizationService.NavGpu, "\uE7F4");
        AddDivider();

        AddNav("Parametres", LocalizationService.NavParametres, "\uE713");
        AddNav("Profil", LocalizationService.NavProfil, "\uE77B");
        AddCreatorNav();
    }

    private void AddCreatorNav()
    {
        var creator = new SidebarNavButton("Creator", LocalizationService.NavCreator, "\uE734", goldLabel: true);
        creator.Root.MouseLeftButtonUp += (_, _) => OfferLinks.Open(OfferLinks.Creator);
        NavPanel.Children.Add(creator.Root);
    }

    private void AddNav(string pageKey, string label, string glyph)
    {
        var nav = new SidebarNavButton(pageKey, label, glyph);
        nav.Root.MouseLeftButtonUp += (_, _) => NavigateTo(pageKey);
        _navButtons[pageKey] = nav;
        NavPanel.Children.Add(nav.Root);
    }

    private void AddDivider()
    {
        NavPanel.Children.Add(new Border
        {
            Height = 1,
            Margin = new Thickness(10, 6, 10, 6),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
        });
    }

    private const double SocialIconSize = 16;
    private const double SocialIconLargeSize = 20;

    private void BuildSocialBar()
    {
        SocialPanel.Children.Clear();

        AddSocial(PanelEndpoints.SocialX, "X",
            SocialIconHelper.CreateIcon("x.png", SocialIconStyle.DarkGlyph, SocialIconSize) ?? MakeSocialFallback("X"));
        AddSocial(PanelEndpoints.SocialTikTok, "TikTok",
            SocialIconHelper.CreateIcon("tiktok.png", SocialIconStyle.LightGlyph, SocialIconLargeSize) ?? MakeSocialFallback("T"));
        AddSocial(PanelEndpoints.SocialYoutube, "YouTube",
            SocialIconHelper.CreateIcon("youtube.png", SocialIconStyle.DarkGlyph, SocialIconSize) ?? MakeSocialFallback("Y"));
        AddSocial(PanelEndpoints.SocialDiscord, "Discord",
            SocialIconHelper.CreateIcon("discord.png", SocialIconStyle.Raw, SocialIconSize) ?? MakeSocialFallback("D"));
        AddSocial(OfferLinks.BuildUrl(OfferLinks.SidebarWebsite), LocalizationService.T("Site web", "Website"),
            SocialIconHelper.CreateIcon("web.png", SocialIconStyle.DarkGlyph, SocialIconLargeSize) ?? MakeSocialFallback("W"));
    }

    private void AddSocial(string url, string tip, UIElement content)
    {
        var hostStyle = (Style)FindResource("SocialIconHost");
        var item = new Border
        {
            Style = hostStyle,
            ToolTip = tip,
            Tag = url,
            Child = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Children = { content },
            },
        };
        if (content is FrameworkElement element)
        {
            element.HorizontalAlignment = HorizontalAlignment.Center;
            element.VerticalAlignment = VerticalAlignment.Center;
        }
        item.MouseLeftButtonUp += (_, _) =>
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        SocialPanel.Children.Add(item);
    }

    private static UIElement MakeSocialFallback(string letter) =>
        new TextBlock
        {
            Text = letter,
            Foreground = Brushes.White,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

    private void LoadAssets()
    {
        ApplySavedBackground();
        ApplySavedAvatar();
        SidebarLogo.Source = LoadCrispImage(AssetHelper.Logo, 140);
        UpdateEasterEggHint();
    }

    public void ApplySavedBackground()
    {
        var profile = UserProfileStore.Instance;
        if (profile.HasCustomBackground)
        {
            SetAppBackgroundFromFile(profile.BackgroundCustomPath!);
            return;
        }

        var fileName = BackgroundCatalog.FileNameFor(profile.BackgroundId)
            ?? BackgroundCatalog.FileNameFor(BackgroundCatalog.DefaultId)
            ?? BackgroundCatalog.All[0].FileName;
        SetAppBackground(fileName);
    }

    public void ApplySavedAvatar()
    {
        var source = UserProfileStore.Instance.ResolveAvatarSource();
        if (source is null)
            return;

        SidebarAvatar.Source = source;
    }

    public void OnBackgroundChanged(string backgroundId, string? customPath = null)
    {
        UserProfileStore.Instance.SaveBackground(backgroundId, customPath);
        UpdateEasterEggHint();
    }

    public void OnBioChanged(string bio)
    {
        UserProfileStore.Instance.SaveBio(bio);
        ApplyDisplayName(UserProfileStore.Instance.ResolvedDisplayName);
    }

    private void UpdateEasterEggHint()
    {
        var show = BackgroundSelectionState.SelectedId == "japan";
        EasterEggHint.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show && !AppSettingsStore.Instance.EasterEggUnlocked)
            AppSettingsStore.Instance.UnlockEasterEgg();
    }

    private bool _onboardingStartupQueued;
    private int _onboardingStep;
    private bool _spotlightPositioning;
    private bool _onboardingTransitioning;
    private bool _onboardingAnimateDims;
    private int _spotlightRetries;
    private DispatcherTimer? _spotlightTimer;

    private static readonly IEasingFunction OnboardingEaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };
    private static readonly IEasingFunction OnboardingEaseInOut = new CubicEase { EasingMode = EasingMode.EaseInOut };
    private static readonly IEasingFunction OnboardingPopEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18 };
    private static readonly TimeSpan OnboardingCardOutDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan OnboardingCardInDuration = TimeSpan.FromMilliseconds(340);
    private static readonly TimeSpan OnboardingDimDuration = TimeSpan.FromMilliseconds(420);
    private static readonly TimeSpan OnboardingOverlayOutDuration = TimeSpan.FromMilliseconds(260);

    private readonly record struct DimRectState(double Left, double Top, double Width, double Height);

    private enum OnboardingCardLayout
    {
        Centered,
        CenteredLeft,
        Bottom,
        CompactTopLeft,
        SidebarGap,
        GpuBelowChoices,
    }

    private void QueueStartupOnboarding()
    {
        if (_onboardingStartupQueued || !UserProfileStore.Instance.HasDisplayName || !ShouldShowOnboarding)
            return;

        _onboardingStartupQueued = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            StartOnboarding();
        };
        timer.Start();
    }

    private void ShowOnboardingIfNeeded()
    {
        if (!ShouldShowOnboarding)
            return;

        StartOnboarding();
    }

    public void RestartOnboarding()
    {
        AppSettingsStore.Instance.ResetOnboarding();
        StartOnboarding();
    }

    private void StartOnboarding()
    {
        NavigateTo("Accueil");
        OnboardingOverlay.Visibility = Visibility.Visible;
        OnboardingOverlay.Opacity = 1;
        _onboardingStep = 0;
        _onboardingTransitioning = false;
        SetOnboardingButtonsEnabled(true);
        ResetOnboardingCardVisuals();
        UpdateOnboardingStep(immediateSpotlight: true);

        Dispatcher.BeginInvoke(() =>
        {
            if (OnboardingOverlay.Visibility != Visibility.Visible)
                return;

            AnimateOnboardingCardIn();
        }, DispatcherPriority.Loaded);
    }

    private void OnboardingOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (OnboardingOverlay.Visibility == Visibility.Visible)
            ScheduleSpotlightPosition();
    }

    private void UpdateOnboardingStep(bool immediateSpotlight = false, bool animateSpotlight = false)
    {
        NavigateForOnboardingStep(_onboardingStep);

        OnboardingTitle.Text = _onboardingStep switch
        {
            0 => LocalizationService.T("Bienvenue sur TweaksLab Community", "Welcome to TweaksLab Community"),
            1 => LocalizationService.T("Ton score PC", "Your PC score"),
            2 => LocalizationService.T("Astuces TweaksLab", "TweaksLab tips"),
            3 => LocalizationService.T("Personnalise ton profil", "Customize your profile"),
            4 => LocalizationService.T("Jeux et GPU", "Games and GPU"),
            _ => LocalizationService.T("C'est parti !", "Let's go!"),
        };

        OnboardingBody.Text = _onboardingStep switch
        {
            0 => LocalizationService.T(
                "L'espace pour suivre l'état de ton PC & découvrir Tweaks Lab",
                "The place to track your PC status & discover Tweaks Lab"),
            1 => LocalizationService.T("L'Accueil affiche ton score et les températures en direct.", "Home shows your score and live temperatures."),
            2 => LocalizationService.T("Tweaks Community testés par l'équipe.", "Community tweaks tested by the team."),
            3 => LocalizationService.T("Profil : avatar, fond, bio et jeux favoris.", "Profile: avatar, background, bio and favorite games."),
            4 => LocalizationService.T("Tout savoir sur comment optimiser tes drivers graphiques !", "Everything you need to know about optimizing your graphics drivers!"),
            _ => LocalizationService.T("Tu pourras relancer ce guide depuis Paramètres.", "You can replay this guide from Settings."),
        };

        OnboardingSkipLabel.Text = LocalizationService.T("Passer", "Skip");
        OnboardingNextLabel.Text = _onboardingStep >= 5
            ? LocalizationService.T("Terminer", "Finish")
            : LocalizationService.T("Suivant", "Next");

        UpdateOnboardingDots();

        if (immediateSpotlight)
        {
            _spotlightRetries = 0;
            _onboardingAnimateDims = animateSpotlight;
            PositionOnboardingSpotlight();
            _onboardingAnimateDims = false;
        }
        else
        {
            ScheduleSpotlightPosition();
        }
    }

    private void NavigateForOnboardingStep(int step)
    {
        switch (step)
        {
            case 0:
            case 1:
                NavigateTo("Accueil");
                break;
            case 2:
                NavigateTo("Astuces");
                if (PageContent.Content is AstucesView astuces)
                    astuces.ScrollToTop();
                break;
            case 3:
                NavigateTo("Profil");
                if (PageContent.Content is ProfilView profil)
                    profil.ScrollToTop();
                break;
            case 4:
                NavigateTo("Gpu");
                if (PageContent.Content is GpuView gpu)
                    gpu.ScrollToTop();
                break;
        }
    }

    private void ScheduleSpotlightPosition()
    {
        _spotlightRetries = 0;
        _spotlightTimer?.Stop();
        _spotlightTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        _spotlightTimer.Tick += (_, _) =>
        {
            _spotlightTimer!.Stop();
            PositionOnboardingSpotlight();
        };
        _spotlightTimer.Start();
    }

    private void PositionOnboardingSpotlight()
    {
        if (OnboardingOverlay.Visibility != Visibility.Visible || _spotlightPositioning)
            return;

        var (surfaceW, surfaceH) = GetOnboardingSurfaceSize();
        if (surfaceW <= 0 || surfaceH <= 0)
        {
            if (++_spotlightRetries <= 15)
            {
                ScheduleSpotlightPosition();
                return;
            }
        }

        _spotlightPositioning = true;
        try
        {
            switch (_onboardingStep)
            {
                case 0:
                    ShowFullDimOverlay();
                    ApplyOnboardingCardLayout(OnboardingCardLayout.Centered);
                    break;
                case 1:
                    if (TryApplyTargetSpotlight(1, padding: 16, dimFullSidebar: true))
                        ApplyOnboardingCardLayout(OnboardingCardLayout.CenteredLeft);
                    break;
                case 2:
                    if (TryApplyPageContentSpotlight(dimFullSidebar: true))
                        ApplyOnboardingCardLayout(OnboardingCardLayout.CompactTopLeft);
                    break;
                case 3:
                    if (TryApplyPageContentSpotlight(dimFullSidebar: true))
                    {
                        ApplyOnboardingCardLayout(OnboardingCardLayout.SidebarGap);
                        PositionSidebarGapCard();
                    }
                    break;
                case 4:
                    if (!TryApplyPageContentSpotlight(dimFullSidebar: true))
                        break;

                    ApplyOnboardingCardLayout(OnboardingCardLayout.GpuBelowChoices);
                    var gpuTarget = ResolveOnboardingTarget(4);
                    if (gpuTarget is not { ActualWidth: > 0, ActualHeight: > 0 })
                    {
                        if (++_spotlightRetries <= 12)
                        {
                            ScheduleSpotlightPosition();
                            return;
                        }
                    }

                    PositionCardBelowTarget(gpuTarget);
                    break;
                case 5:
                    ShowFullDimOverlay();
                    ApplyOnboardingCardLayout(OnboardingCardLayout.Centered);
                    break;
            }
        }
        finally
        {
            _spotlightPositioning = false;
        }
    }

    private bool TryApplyTargetSpotlight(int step, double padding, bool dimFullSidebar)
    {
        var target = ResolveOnboardingTarget(step);
        if (target is not { ActualWidth: > 0, ActualHeight: > 0 })
        {
            if (++_spotlightRetries <= 12)
            {
                ScheduleSpotlightPosition();
                return false;
            }

            ShowFullDimOverlay();
            return true;
        }

        var bounds = GetElementBounds(target);
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            if (++_spotlightRetries <= 12)
            {
                ScheduleSpotlightPosition();
                return false;
            }

            ShowFullDimOverlay();
            return true;
        }

        ApplyOnboardingSpotlight(InflateRect(bounds, padding), dimFullSidebar);
        return true;
    }

    private bool TryApplyPageContentSpotlight(bool dimFullSidebar)
    {
        const double sidebarFallback = 200;

        var (canvasW, canvasH) = GetOnboardingSurfaceSize();
        if (canvasW <= 0 || canvasH <= 0)
            return false;

        if (PageContent.ActualWidth <= 0 || PageContent.ActualHeight <= 0)
        {
            if (++_spotlightRetries <= 15)
            {
                ScheduleSpotlightPosition();
                return false;
            }
        }

        var bounds = GetElementBounds(PageContent);
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            bounds = new Rect(sidebarFallback, 0, Math.Max(0, canvasW - sidebarFallback), canvasH);

        var hole = new Rect(
            Math.Max(0, bounds.X),
            Math.Max(0, bounds.Y),
            Math.Min(bounds.Width, canvasW - Math.Max(0, bounds.X)),
            Math.Min(bounds.Height, canvasH - Math.Max(0, bounds.Y)));

        ApplyOnboardingSpotlight(hole, dimFullSidebar);
        return true;
    }

    private void ApplyOnboardingCardLayout(OnboardingCardLayout layout)
    {
        OnboardingTitle.FontSize = 18;
        OnboardingBody.FontSize = 13;
        OnboardingDots.Visibility = Visibility.Visible;

        Grid.SetRow(OnboardingCard, 0);
        Grid.SetRowSpan(OnboardingCard, 2);
        Grid.SetColumnSpan(OnboardingCard, 2);
        Grid.SetColumn(OnboardingCard, 0);

        Panel.SetZIndex(OnboardingCard, 10);

        switch (layout)
        {
            case OnboardingCardLayout.Centered:
                OnboardingCard.MaxWidth = 440;
                OnboardingCard.MinWidth = 300;
                OnboardingCard.Width = double.NaN;
                OnboardingCard.Padding = new Thickness(32, 28, 32, 24);
                OnboardingCard.HorizontalAlignment = HorizontalAlignment.Center;
                OnboardingCard.VerticalAlignment = VerticalAlignment.Center;
                OnboardingCard.Margin = new Thickness(24);
                break;

            case OnboardingCardLayout.CenteredLeft:
                OnboardingCard.MaxWidth = 440;
                OnboardingCard.MinWidth = 300;
                OnboardingCard.Width = double.NaN;
                OnboardingCard.Padding = new Thickness(32, 28, 32, 24);
                OnboardingCard.HorizontalAlignment = HorizontalAlignment.Center;
                OnboardingCard.VerticalAlignment = VerticalAlignment.Center;
                OnboardingCard.Margin = new Thickness(-44, 24, 44, 24);
                break;

            case OnboardingCardLayout.Bottom:
                OnboardingCard.MaxWidth = 420;
                OnboardingCard.MinWidth = 300;
                OnboardingCard.Width = double.NaN;
                OnboardingCard.Padding = new Thickness(28, 24, 28, 24);
                OnboardingCard.HorizontalAlignment = HorizontalAlignment.Center;
                OnboardingCard.VerticalAlignment = VerticalAlignment.Bottom;
                OnboardingCard.Margin = new Thickness(20, 0, 20, 16);
                break;

            case OnboardingCardLayout.CompactTopLeft:
                OnboardingTitle.FontSize = 14;
                OnboardingBody.FontSize = 11;
                Grid.SetColumn(OnboardingCard, 0);
                Grid.SetColumnSpan(OnboardingCard, 1);
                OnboardingCard.MaxWidth = 248;
                OnboardingCard.MinWidth = 0;
                OnboardingCard.Width = double.NaN;
                OnboardingCard.Padding = new Thickness(14, 12, 14, 12);
                OnboardingCard.HorizontalAlignment = HorizontalAlignment.Right;
                OnboardingCard.VerticalAlignment = VerticalAlignment.Top;
                OnboardingCard.Margin = new Thickness(0, 52, -36, 0);
                break;

            case OnboardingCardLayout.SidebarGap:
                OnboardingTitle.FontSize = 15;
                OnboardingBody.FontSize = 12;
                Grid.SetColumn(OnboardingCard, 0);
                Grid.SetColumnSpan(OnboardingCard, 2);
                OnboardingCard.MaxWidth = 220;
                OnboardingCard.MinWidth = 0;
                OnboardingCard.Width = double.NaN;
                OnboardingCard.Padding = new Thickness(16, 16, 16, 14);
                OnboardingCard.HorizontalAlignment = HorizontalAlignment.Left;
                OnboardingCard.VerticalAlignment = VerticalAlignment.Center;
                OnboardingCard.Margin = new Thickness(90, 0, 0, 0);
                Panel.SetZIndex(OnboardingCard, 20);
                break;

            case OnboardingCardLayout.GpuBelowChoices:
                OnboardingTitle.FontSize = 16;
                OnboardingBody.FontSize = 12;
                OnboardingCard.MaxWidth = 320;
                OnboardingCard.MinWidth = 240;
                OnboardingCard.Width = double.NaN;
                OnboardingCard.Padding = new Thickness(20, 18, 20, 16);
                OnboardingCard.HorizontalAlignment = HorizontalAlignment.Left;
                OnboardingCard.VerticalAlignment = VerticalAlignment.Top;
                OnboardingCard.Margin = new Thickness(0, 0, 0, 0);
                break;
        }
    }

    private void PositionCardBelowTarget(FrameworkElement? target, double gap = 16)
    {
        if (target is not { ActualWidth: > 0, ActualHeight: > 0 })
            return;

        var bounds = GetElementBounds(target);
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        OnboardingCard.UpdateLayout();
        OnboardingCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var maxW = double.IsNaN(OnboardingCard.MaxWidth) ? 400 : OnboardingCard.MaxWidth;
        var cardW = Math.Min(OnboardingCard.DesiredSize.Width, maxW);

        var (surfaceW, _) = GetOnboardingSurfaceSize();
        var left = bounds.X + (bounds.Width - cardW) / 2;
        if (surfaceW > cardW + 16)
            left = Math.Max(8, Math.Min(left, surfaceW - cardW - 8));

        OnboardingCard.Margin = new Thickness(left, bounds.Bottom + gap, 8, 0);
    }

    private void PositionSidebarGapCard()
    {
        const double sidebarWidth = 200;

        OnboardingCard.UpdateLayout();
        OnboardingCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var maxW = double.IsNaN(OnboardingCard.MaxWidth) ? 220 : OnboardingCard.MaxWidth;
        var cardW = Math.Min(OnboardingCard.DesiredSize.Width, maxW);
        var left = sidebarWidth - (cardW / 2);

        OnboardingCard.Margin = new Thickness(Math.Max(6, left), 0, 0, 0);
    }

    private (double Width, double Height) GetOnboardingSurfaceSize()
    {
        var w = OnboardingOverlay.ActualWidth;
        var h = OnboardingOverlay.ActualHeight;
        if (w <= 0 || h <= 0)
        {
            w = OnboardingCanvas.ActualWidth;
            h = OnboardingCanvas.ActualHeight;
        }

        return (w, h);
    }

    private FrameworkElement? ResolveOnboardingTarget(int step) => step switch
    {
        0 or 5 => null,
        1 when PageContent.Content is HomeView home => home.GetOnboardingTarget(step),
        2 when PageContent.Content is AstucesView astuces => astuces.GetOnboardingTarget(0),
        3 when PageContent.Content is ProfilView profil => profil.GetOnboardingTarget(0),
        4 when PageContent.Content is GpuView gpu => gpu.GetOnboardingTarget(0),
        2 when _navButtons.TryGetValue("Astuces", out var astucesNav) => astucesNav.Root,
        4 when _navButtons.TryGetValue("Gpu", out var gpuNav) => gpuNav.Root,
        _ => null,
    };

    private void ShowFullDimOverlay()
    {
        var (w, h) = GetOnboardingSurfaceSize();
        if (w <= 0 || h <= 0)
            return;

        ApplyDimRects(
            new DimRectState(0, 0, w, h),
            EmptyDimRect,
            EmptyDimRect,
            EmptyDimRect,
            _onboardingAnimateDims);
    }

    private void ApplyOnboardingSpotlight(Rect hole, bool dimFullSidebar = false)
    {
        var (w, h) = GetOnboardingSurfaceSize();
        if (w <= 0 || h <= 0)
            return;

        if (dimFullSidebar)
        {
            ApplyDimRects(
                new DimRectState(0, 0, w, Math.Max(0, hole.Top)),
                new DimRectState(0, hole.Top, Math.Max(0, hole.Left), hole.Height),
                new DimRectState(hole.Right, hole.Top, Math.Max(0, w - hole.Right), hole.Height),
                new DimRectState(0, hole.Bottom, w, Math.Max(0, h - hole.Bottom)),
                _onboardingAnimateDims);
            return;
        }

        ApplyDimRects(
            new DimRectState(0, 0, w, Math.Max(0, hole.Top)),
            new DimRectState(0, hole.Top, Math.Max(0, hole.Left), hole.Height),
            new DimRectState(hole.Right, hole.Top, Math.Max(0, w - hole.Right), hole.Height),
            new DimRectState(0, hole.Bottom, w, Math.Max(0, h - hole.Bottom)),
            _onboardingAnimateDims);
    }

    private static DimRectState EmptyDimRect => new(0, 0, 0, 0);

    private void ApplyDimRects(
        DimRectState top,
        DimRectState left,
        DimRectState right,
        DimRectState bottom,
        bool animate)
    {
        if (!animate)
        {
            SetDimState(DimTop, top);
            SetDimState(DimLeft, left);
            SetDimState(DimRight, right);
            SetDimState(DimBottom, bottom);
            return;
        }

        AnimateDimState(DimTop, top);
        AnimateDimState(DimLeft, left);
        AnimateDimState(DimRight, right);
        AnimateDimState(DimBottom, bottom);
    }

    private static void SetDimState(Rectangle rect, DimRectState state)
    {
        Canvas.SetLeft(rect, state.Left);
        Canvas.SetTop(rect, state.Top);
        rect.Width = state.Width;
        rect.Height = state.Height;
    }

    private static DimRectState GetDimState(Rectangle rect) =>
        new(
            double.IsNaN(Canvas.GetLeft(rect)) ? 0 : Canvas.GetLeft(rect),
            double.IsNaN(Canvas.GetTop(rect)) ? 0 : Canvas.GetTop(rect),
            rect.Width,
            rect.Height);

    private void AnimateDimState(Rectangle rect, DimRectState target)
    {
        var current = GetDimState(rect);
        AnimateDimProperty(rect, Canvas.LeftProperty, current.Left, target.Left);
        AnimateDimProperty(rect, Canvas.TopProperty, current.Top, target.Top);
        AnimateDimProperty(rect, Rectangle.WidthProperty, current.Width, target.Width);
        AnimateDimProperty(rect, Rectangle.HeightProperty, current.Height, target.Height);
    }

    private void AnimateDimProperty(Rectangle rect, DependencyProperty property, double from, double to)
    {
        rect.BeginAnimation(property, new DoubleAnimation(from, to, OnboardingDimDuration)
        {
            EasingFunction = OnboardingEaseInOut,
            FillBehavior = FillBehavior.HoldEnd,
        });
    }

    private void ResetOnboardingCardVisuals()
    {
        OnboardingCard.BeginAnimation(UIElement.OpacityProperty, null);
        OnboardingCardScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        OnboardingCardScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        OnboardingCardTranslate.BeginAnimation(TranslateTransform.YProperty, null);

        OnboardingCard.Opacity = 1;
        OnboardingCardScale.ScaleX = 1;
        OnboardingCardScale.ScaleY = 1;
        OnboardingCardTranslate.Y = 0;
    }

    private void SetOnboardingButtonsEnabled(bool enabled)
    {
        OnboardingSkipBtn.IsEnabled = enabled;
        OnboardingNextBtn.IsEnabled = enabled;
    }

    private void AnimateOnboardingCardOut(Action onComplete)
    {
        var storyboard = new Storyboard { FillBehavior = FillBehavior.Stop };
        storyboard.Completed += (_, _) => onComplete();

        AddCardAnimation(storyboard, OnboardingCard, UIElement.OpacityProperty, OnboardingCard.Opacity, 0, OnboardingCardOutDuration, OnboardingEaseOut);
        AddCardAnimation(storyboard, OnboardingCardTranslate, TranslateTransform.YProperty, OnboardingCardTranslate.Y, 12, OnboardingCardOutDuration, OnboardingEaseOut);
        AddCardAnimation(storyboard, OnboardingCardScale, ScaleTransform.ScaleXProperty, OnboardingCardScale.ScaleX, 0.96, OnboardingCardOutDuration, OnboardingEaseOut);
        AddCardAnimation(storyboard, OnboardingCardScale, ScaleTransform.ScaleYProperty, OnboardingCardScale.ScaleY, 0.96, OnboardingCardOutDuration, OnboardingEaseOut);
        storyboard.Begin();
    }

    private void AnimateOnboardingCardIn(Action? onComplete = null)
    {
        OnboardingCard.BeginAnimation(UIElement.OpacityProperty, null);
        OnboardingCardScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        OnboardingCardScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        OnboardingCardTranslate.BeginAnimation(TranslateTransform.YProperty, null);

        OnboardingCard.Opacity = 0;
        OnboardingCardScale.ScaleX = 0.94;
        OnboardingCardScale.ScaleY = 0.94;
        OnboardingCardTranslate.Y = -12;

        var storyboard = new Storyboard { FillBehavior = FillBehavior.HoldEnd };
        storyboard.Completed += (_, _) =>
        {
            ResetOnboardingCardVisuals();
            onComplete?.Invoke();
        };

        AddCardAnimation(storyboard, OnboardingCard, UIElement.OpacityProperty, 0, 1, OnboardingCardInDuration, OnboardingEaseOut);
        AddCardAnimation(storyboard, OnboardingCardTranslate, TranslateTransform.YProperty, -12, 0, OnboardingCardInDuration, OnboardingPopEase);
        AddCardAnimation(storyboard, OnboardingCardScale, ScaleTransform.ScaleXProperty, 0.94, 1, OnboardingCardInDuration, OnboardingPopEase);
        AddCardAnimation(storyboard, OnboardingCardScale, ScaleTransform.ScaleYProperty, 0.94, 1, OnboardingCardInDuration, OnboardingPopEase);
        storyboard.Begin();
    }

    private void AnimateOnboardingOverlayOut(Action onComplete)
    {
        var storyboard = new Storyboard { FillBehavior = FillBehavior.Stop };
        storyboard.Completed += (_, _) => onComplete();

        AddCardAnimation(storyboard, OnboardingOverlay, UIElement.OpacityProperty, OnboardingOverlay.Opacity, 0, OnboardingOverlayOutDuration, OnboardingEaseOut);
        AddCardAnimation(storyboard, OnboardingCard, UIElement.OpacityProperty, OnboardingCard.Opacity, 0, OnboardingOverlayOutDuration, OnboardingEaseOut);
        AddCardAnimation(storyboard, OnboardingCardScale, ScaleTransform.ScaleXProperty, OnboardingCardScale.ScaleX, 0.95, OnboardingOverlayOutDuration, OnboardingEaseOut);
        AddCardAnimation(storyboard, OnboardingCardScale, ScaleTransform.ScaleYProperty, OnboardingCardScale.ScaleY, 0.95, OnboardingOverlayOutDuration, OnboardingEaseOut);
        storyboard.Begin();
    }

    private static void AddCardAnimation(
        Storyboard storyboard,
        DependencyObject target,
        DependencyProperty property,
        double from,
        double to,
        TimeSpan duration,
        IEasingFunction easing)
    {
        var animation = new DoubleAnimation(from, to, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd,
        };

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(property));
        storyboard.Children.Add(animation);
    }

    private void AdvanceOnboardingStep()
    {
        if (_onboardingTransitioning)
            return;

        _onboardingTransitioning = true;
        SetOnboardingButtonsEnabled(false);

        AnimateOnboardingCardOut(() =>
        {
            _onboardingStep++;
            UpdateOnboardingStep(immediateSpotlight: true, animateSpotlight: true);
            AnimateOnboardingCardIn(() =>
            {
                _onboardingTransitioning = false;
                SetOnboardingButtonsEnabled(true);
            });
        });
    }

    private void FinishOnboarding()
    {
        if (_onboardingTransitioning)
            return;

        _onboardingTransitioning = true;
        SetOnboardingButtonsEnabled(false);
        CompleteOnboardingIfNeeded();

        AnimateOnboardingOverlayOut(() =>
        {
            _onboardingTransitioning = false;
            EndOnboarding();
        });
    }

    private Rect GetElementBounds(FrameworkElement element)
    {
        try
        {
            var transform = element.TransformToVisual(OnboardingCanvas);
            var topLeft = transform.Transform(new Point(0, 0));
            var bottomRight = transform.Transform(new Point(element.ActualWidth, element.ActualHeight));
            return new Rect(topLeft, bottomRight);
        }
        catch
        {
            return Rect.Empty;
        }
    }

    private static Rect InflateRect(Rect rect, double padding) =>
        new(rect.X - padding, rect.Y - padding, rect.Width + padding * 2, rect.Height + padding * 2);

    private void EndOnboarding()
    {
        _spotlightTimer?.Stop();
        OnboardingOverlay.BeginAnimation(UIElement.OpacityProperty, null);
        OnboardingOverlay.Opacity = 1;
        ResetOnboardingCardVisuals();
        OnboardingOverlay.Visibility = Visibility.Collapsed;
        NavigateTo("Accueil");
    }

    private void UpdateOnboardingDots()
    {
        OnboardingDots.Children.Clear();
        var active = (Brush)FindResource("AccentBrush");
        var idle = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        idle.Freeze();

        for (var i = 0; i < 6; i++)
        {
            OnboardingDots.Children.Add(new Ellipse
            {
                Width = 7,
                Height = 7,
                Margin = new Thickness(4, 0, 4, 0),
                Fill = i == _onboardingStep ? active : idle,
            });
        }
    }

    private void OnboardingNext_Click(object sender, RoutedEventArgs e)
    {
        if (_onboardingStep >= 5)
        {
            FinishOnboarding();
            return;
        }

        AdvanceOnboardingStep();
    }

    private void OnboardingSkip_Click(object sender, RoutedEventArgs e)
    {
        FinishOnboarding();
    }

    private static BitmapImage LoadCrispImage(string path, int decodeWidth)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.DecodePixelWidth = decodeWidth;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public void SetSidebarAvatar(string fileName)
    {
        SidebarAvatar.Source = new BitmapImage(new Uri(AssetHelper.Avatar(fileName)));
    }

    public void SetSidebarAvatarImage(ImageSource source)
    {
        SidebarAvatar.Source = source;
    }

    public void NavigateTo(string page)
    {
        _escapeBackPage = null;
        _currentPage = page;
        var isReturning = _pages.ContainsKey(page);

        if (!_pages.ContainsKey(page))
        {
            _pages[page] = page switch
            {
                "Accueil" => new HomeView(),
                "MonPc" => new MonPcView(),
                "Jeux" => new JeuxView(),
                "Application" => new ApplicationView(),
                "Nettoyer" => new NettoyerView(),
                "Reinstallation" => new ReinstallationView(),
                "Recommandations" => new RecommandationsView(),
                "Astuces" => new AstucesView(),
                "Gpu" => new GpuView(),
                "Assistance" => new AssistanceView(),
                "Parametres" => new ParametresView(),
                "Profil" => new ProfilView(),
                "Score" => new ScoreDetailView(),
                _ => new HomeView()
            };
        }

        var leavingAccueil = PageContent.Content is HomeView && page != "Accueil";

        PageContent.Content = _pages[page];

        if (page == "Accueil" && _pages[page] is HomeView homePage)
            homePage.SetDisplayName(UserProfileStore.Instance.ResolvedDisplayName);
        else if (page == "Profil" && _pages[page] is ProfilView profilPage)
        {
            profilPage.SetDisplayName(UserProfileStore.Instance.ResolvedDisplayName);
            profilPage.RefreshBackgroundWallet();
        }
        else if (page == "Parametres" && _pages[page] is ParametresView parametresPage)
            parametresPage.RefreshBiosFeatures();

        if (leavingAccueil && _pages.TryGetValue("Accueil", out var accueilPage) && accueilPage is HomeView pausedHome)
        {
            pausedHome.PauseBackgroundCarousel();
            pausedHome.PauseWallpaperCarousel();
        }

        // Le détail du score dépend de l'accueil : bouton Accueil actif, Échap pour y revenir.
        var activeNav = page == "Score" ? "Accueil" : page;
        foreach (var (key, nav) in _navButtons)
            nav.SetActive(key == activeNav);

        if (page == "Score")
        {
            _escapeBackPage = "Accueil";
            if (_pages[page] is ScoreDetailView scoreDetail)
                scoreDetail.ScrollToTop();
        }

        if (page == "Accueil" && _pages[page] is HomeView home)
        {
            home.ResumeBackgroundCarousel();
            home.ResumeWallpaperCarousel();
            if (isReturning)
                home.RefreshOnShow();
        }
    }

    public void NavigateToProfilTop()
    {
        NavigateTo("Profil");
        if (PageContent.Content is ProfilView profil)
            profil.ScrollToTop();
    }

    public void NavigateToProfilPersonalization()
    {
        NavigateTo("Profil");
        if (_pages["Profil"] is ProfilView profil)
            profil.ScrollToPersonalization();
    }

    public void NavigateToWallpaperLibrary(string returnTo = "Profil", bool scrollToBottom = false)
    {
        if (!_pages.ContainsKey("BibliothequeFondsEcran"))
            _pages["BibliothequeFondsEcran"] = new WallpaperLibraryView();

        if (_pages["BibliothequeFondsEcran"] is WallpaperLibraryView library)
        {
            library.ConfigureReturn(returnTo);
            library.RefreshSelection();
            if (scrollToBottom)
                library.ScrollToBottom();
        }

        PageContent.Content = _pages["BibliothequeFondsEcran"];
        _currentPage = "BibliothequeFondsEcran";
        _wallpaperLibraryReturnPage = returnTo;
        _escapeBackPage = returnTo;

        foreach (var (key, nav) in _navButtons)
            nav.SetActive(key == returnTo);
    }

    public void NavigateToBackgroundLibrary(string returnTo = "Profil")
    {
        if (!_pages.ContainsKey("BibliothequeFonds"))
            _pages["BibliothequeFonds"] = new BackgroundLibraryView();

        if (_pages["BibliothequeFonds"] is BackgroundLibraryView library)
        {
            library.ConfigureReturn(returnTo);
            library.RefreshSelection();
        }

        PageContent.Content = _pages["BibliothequeFonds"];
        _currentPage = "BibliothequeFonds";
        _backgroundLibraryReturnPage = returnTo;
        _escapeBackPage = returnTo;

        foreach (var (key, nav) in _navButtons)
            nav.SetActive(key == returnTo);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        if (PseudoOverlay.Visibility == Visibility.Visible)
            return;

        if (Keyboard.FocusedElement is TextBoxBase)
            return;

        if (PageContent.Content is GpuView gpu && gpu.TryEscapeBack())
        {
            e.Handled = true;
            return;
        }

        if (_escapeBackPage is null)
            return;

        NavigateTo(_escapeBackPage);
        e.Handled = true;
    }

    public void RefreshPage(string page)
    {
        _pages.Remove(page);
        NavigateTo(page);
    }

    private void ProfileCard_Click(object sender, MouseButtonEventArgs e)
    {
        NavigateToProfilTop();
        e.Handled = true;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    public void SetAppBackground(string fileName)
    {
        BackgroundImage.Source = BitmapLoader.LoadFull(AssetHelper.Background(fileName));
        BackgroundMotionHelper.Attach(BackgroundImage, BackgroundMotionProfile.Full);
        var preset = BackgroundCatalog.All.FirstOrDefault(p => p.FileName == fileName);
        if (preset is not null)
        {
            BackgroundSelectionState.SelectedId = preset.Id;
            BackgroundSelectionState.IsCustom = false;
            BackgroundSelectionState.CustomPath = null;
        }

        UpdateEasterEggHint();
    }

    public void SetAppBackgroundFromFile(string filePath)
    {
        BackgroundImage.Source = BitmapLoader.LoadFull(filePath);
        BackgroundMotionHelper.Attach(BackgroundImage, BackgroundMotionProfile.Full);
        BackgroundSelectionState.IsCustom = true;
        BackgroundSelectionState.CustomPath = filePath;
        UpdateEasterEggHint();
    }

    private void Notif_Click(object sender, RoutedEventArgs e)
    {
        if (_notifAnimating) return;

        if (_notifOpen)
            AnimateNotifClose();
        else
            AnimateNotifOpen();

        AnimateBellPulse();
    }

    private void RootContent_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_notifOpen || _notifAnimating) return;
        if (e.OriginalSource is not DependencyObject src) return;
        if (IsWithinVisualTree(src, NotifBtnHost)) return;
        if (IsWithinPopup(src)) return;

        AnimateNotifClose();
    }

    private bool IsWithinPopup(DependencyObject source)
    {
        if (NotifPopup.Child is not DependencyObject popupRoot)
            return false;

        while (source != null)
        {
            if (source == popupRoot) return true;
            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }

        return false;
    }

    private static bool IsWithinVisualTree(DependencyObject source, DependencyObject ancestor)
    {
        while (source != null)
        {
            if (source == ancestor) return true;
            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }

        return false;
    }

    private void AnimateNotifOpen()
    {
        NotifScale.ScaleX = NotifScale.ScaleY = 0.86;
        NotifTranslate.Y = -10;
        NotifPanel.Opacity = 0;
        NotifPopup.IsOpen = true;
        _notifOpen = true;
        SetNotificationCount(0);

        _notifAnimating = true;
        var dur = TimeSpan.FromMilliseconds(260);

        var fade = new DoubleAnimation(0, 1, dur) { EasingFunction = NotifEaseOut, FillBehavior = FillBehavior.HoldEnd };
        fade.Completed += (_, _) => _notifAnimating = false;

        NotifPanel.BeginAnimation(UIElement.OpacityProperty, fade);
        NotifScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.86, 1, dur) { EasingFunction = NotifPopEase, FillBehavior = FillBehavior.HoldEnd });
        NotifScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.86, 1, dur) { EasingFunction = NotifPopEase, FillBehavior = FillBehavior.HoldEnd });
        NotifTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-10, 0, dur) { EasingFunction = NotifEaseOut, FillBehavior = FillBehavior.HoldEnd });
    }

    private void AnimateNotifClose()
    {
        _notifAnimating = true;
        var dur = TimeSpan.FromMilliseconds(190);

        var fade = new DoubleAnimation(NotifPanel.Opacity, 0, dur) { EasingFunction = NotifEaseIn };
        fade.Completed += (_, _) =>
        {
            NotifPopup.IsOpen = false;
            _notifOpen = false;
            ResetNotifPanelVisual();
            _notifAnimating = false;
        };

        NotifPanel.BeginAnimation(UIElement.OpacityProperty, fade);
        NotifScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, 0.9, dur) { EasingFunction = NotifEaseIn });
        NotifScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1, 0.9, dur) { EasingFunction = NotifEaseIn });
        NotifTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, -8, dur) { EasingFunction = NotifEaseIn });
    }

    private void NotifPopup_Closed(object? sender, EventArgs e)
    {
        _notifOpen = false;
        ResetNotifPanelVisual();
    }

    private void ResetNotifPanelVisual()
    {
        NotifPanel.BeginAnimation(UIElement.OpacityProperty, null);
        NotifScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        NotifScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        NotifTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        NotifPanel.Opacity = 1;
        NotifScale.ScaleX = NotifScale.ScaleY = 1;
        NotifTranslate.Y = 0;
        _notifAnimating = false;
    }

    private void AnimateBellPulse()
    {
        if (NotifBtnHost.RenderTransform is not ScaleTransform scale)
            return;

        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, 0.9, TimeSpan.FromMilliseconds(90))
            {
                AutoReverse = true,
                EasingFunction = NotifEaseOut,
                FillBehavior = FillBehavior.Stop,
            });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1, 0.9, TimeSpan.FromMilliseconds(90))
            {
                AutoReverse = true,
                EasingFunction = NotifEaseOut,
                FillBehavior = FillBehavior.Stop,
            });
    }

    public void SetNotificationCount(int count)
    {
        if (count <= 0)
        {
            NotifBadge.Visibility = Visibility.Collapsed;
            return;
        }

        NotifBadgeText.Text = count > 9 ? "9+" : count.ToString();
        NotifBadge.Visibility = Visibility.Visible;
    }
}
