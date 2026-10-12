using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;
using TweaksLabPanel.Services;

namespace TweaksLabPanel.Views;

public partial class GpuView : UserControl
{
    private static readonly SolidColorBrush StepBorderBrush = new(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush StepBackgroundBrush = new(Color.FromArgb(0x1A, 0x14, 0x10, 0x22));
    private static readonly SolidColorBrush StepBadgeBrush = new(Color.FromArgb(0x44, 0xA8, 0x55, 0xF7));

    private DetectedGpu? _detected;
    private bool _forceSelection;

    static GpuView()
    {
        StepBorderBrush.Freeze();
        StepBackgroundBrush.Freeze();
        StepBadgeBrush.Freeze();
    }

    public GpuView()
    {
        InitializeComponent();
        NvidiaLogo.Source = LoadGpuLogo("nvidia.png");
        AmdLogo.Source = LoadGpuLogo("amd.png");
        GpuOfferText.Text = LocalizationService.T(
            $"L’équipe TweaksLab optimise ta carte graphique avec le reste de ton PC, {OfferLinks.StartingPrice}.",
            $"The TweaksLab team optimizes your graphics card along with the rest of your PC, {OfferLinks.StartingPrice}.");

        // Détection lancée au démarrage du panel : généralement déjà terminée ici.
        var detection = GpuDetector.DetectAsync();
        if (detection.IsCompletedSuccessfully)
            _detected = detection.Result;

        RefreshViewState();
        Loaded += async (_, _) =>
        {
            _detected = await GpuDetector.DetectAsync();
            RefreshViewState();
        };
    }

    private void NvidiaCard_Click(object sender, MouseButtonEventArgs e) => SelectVendor(GpuVendor.Nvidia);

    private void AmdCard_Click(object sender, MouseButtonEventArgs e) => SelectVendor(GpuVendor.Amd);

    private void ChangeVendor_Click(object sender, RoutedEventArgs e) => ShowSelection();

    private void GpuOffer_Click(object sender, RoutedEventArgs e) => OfferLinks.Open(OfferLinks.Gpu);

    private void SelectVendor(string vendor)
    {
        GpuVendorStore.Instance.Save(vendor);
        _forceSelection = false;
        RefreshViewState();
    }

    private void ShowSelection()
    {
        GpuVendorStore.Instance.Clear();
        _forceSelection = true;
        RefreshViewState();
        ScrollToTop();
    }

    /// <summary>Choix manuel en priorité, sinon la carte détectée (NVIDIA / AMD).</summary>
    private string? ResolveVendor()
    {
        var stored = GpuVendorStore.Instance.SelectedVendor;
        if (stored is GpuVendor.Nvidia or GpuVendor.Amd)
            return stored;

        if (_forceSelection)
            return null;

        return _detected?.Vendor is GpuVendor.Nvidia or GpuVendor.Amd ? _detected.Vendor : null;
    }

    private bool IsShowingDetectedCard(string vendor) =>
        GpuVendorStore.Instance.SelectedVendor is null && _detected?.Vendor == vendor;

    private void RefreshViewState()
    {
        var vendor = ResolveVendor();
        var hasSelection = vendor is GpuVendor.Nvidia or GpuVendor.Amd;

        SelectionPanel.Visibility = hasSelection ? Visibility.Collapsed : Visibility.Visible;
        StepsPanel.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;

        PageSubtitleText.Text = hasSelection
            ? LocalizationService.T("Suis les étapes dans l’ordre pour optimiser ta carte graphique.", "Follow the steps in order to optimize your graphics card.")
            : LocalizationService.T("Sélectionne ta carte graphique pour commencer l’optimisation.", "Select your graphics card to start optimizing.");

        RefreshDetectionNote();

        if (!hasSelection)
            return;

        SelectedVendorLogo.Source = LoadGpuLogo(vendor == GpuVendor.Nvidia ? "nvidia.png" : "amd.png");
        // Nom exact de la carte dès qu'elle correspond à la marque affichée (choix manuel ou détection).
        SelectedVendorTitle.Text = _detected is { } detected && detected.Vendor == vendor && !string.IsNullOrWhiteSpace(detected.Name)
            ? detected.Name
            : vendor == GpuVendor.Nvidia ? "NVIDIA" : "AMD";
        SelectedVendorSubtitle.Text = IsShowingDetectedCard(vendor!)
            ? LocalizationService.T("Carte détectée automatiquement · optimisation GPU TweaksLab Community", "Card detected automatically · TweaksLab Community GPU optimization")
            : LocalizationService.T("Optimisation GPU TweaksLab Community", "TweaksLab Community GPU optimization");
        BuildSteps(vendor!);
    }

    private void RefreshDetectionNote()
    {
        string? note = _detected switch
        {
            { Vendor: GpuVendor.Intel } gpu => LocalizationService.T(
                $"Carte détectée : {gpu.Name}. Le guide Intel arrive bientôt. Si ton PC a aussi une carte NVIDIA ou AMD, choisis-la ci-dessus.",
                $"Detected card: {gpu.Name}. The Intel guide is coming soon. If your PC also has an NVIDIA or AMD card, pick it above."),
            { } gpu => LocalizationService.T($"Carte détectée : {gpu.Name}", $"Detected card: {gpu.Name}"),
            null => null,
        };

        DetectionNoteText.Text = note ?? string.Empty;
        DetectionNoteText.Visibility = note is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void BuildSteps(string vendor)
    {
        StepsHost.Children.Clear();

        if (vendor == GpuVendor.Nvidia)
            BuildNvidiaSteps();
        else
            BuildAmdSteps();
    }

    private void BuildNvidiaSteps()
    {
        StepsHost.Children.Add(CreateStepCard(
            1,
            LocalizationService.T("Réinstaller ses pilotes (DDU + Install)", "Reinstall your drivers (DDU + install)"),
            LocalizationService.T(
                "Pour une installation propre, désinstalle d'abord tes anciens pilotes avec DDU, puis réinstalle la dernière version depuis le site NVIDIA.",
                "For a clean install, first remove your old drivers with DDU, then install the latest version from the NVIDIA website."),
            CreateDduVideoSection(PanelEndpoints.GpuNvidiaDduVideoUrl)));

        StepsHost.Children.Add(CreateStepCard(
            2,
            LocalizationService.T("Optimiser le Panneau de configuration NVIDIA", "Optimize the NVIDIA Control Panel"),
            LocalizationService.T(
                "Suis les captures dans l'ordre. Ouvre le panneau NVIDIA, puis applique les mêmes réglages dans Paramètres 3D → Paramètres globaux.",
                "Follow the screenshots in order. Open the NVIDIA panel, then apply the same settings in 3D Settings → Global Settings."),
            CreateNvidiaPanelGuide()));
    }

    private void BuildAmdSteps()
    {
        StepsHost.Children.Add(CreateStepCard(
            1,
            LocalizationService.T("Réinstaller ses pilotes (DDU + Install)", "Reinstall your drivers (DDU + install)"),
            LocalizationService.T(
                "Pour une installation propre, désinstalle d'abord tes anciens pilotes avec DDU, puis réinstalle la dernière version depuis le site AMD.",
                "For a clean install, first remove your old drivers with DDU, then install the latest version from the AMD website."),
            CreateDduVideoSection(PanelEndpoints.GpuAmdDduVideoUrl)));

        StepsHost.Children.Add(CreateStepCard(
            2,
            LocalizationService.T("Optimiser AMD Software : Adrenalin Edition", "Optimize AMD Software: Adrenalin Edition"),
            LocalizationService.T(
                "Suis les captures dans l'ordre. Ouvre AMD Software, puis reproduis les mêmes réglages dans chaque section.",
                "Follow the screenshots in order. Open AMD Software, then copy the same settings in each section."),
            CreateAmdPanelGuide()));
    }

    private static UIElement CreateDduVideoSection(string videoUrl)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        var hasUrl = !string.IsNullOrWhiteSpace(videoUrl);

        var button = new Button
        {
            Style = (Style)Application.Current.FindResource("OutlineButton"),
            Padding = new Thickness(18, 10, 18, 10),
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = hasUrl,
            Content = LocalizationService.T("Voir le tutoriel YouTube", "Watch the YouTube tutorial"),
        };

        if (hasUrl)
            button.Click += (_, _) => OpenUrl(videoUrl);

        stack.Children.Add(button);

        if (!hasUrl)
        {
            stack.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("Le lien vidéo sera ajouté prochainement.", "The video link will be added soon."),
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 12,
                Foreground = Brushes.White,
                Opacity = 0.65,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return stack;
    }

    private static UIElement CreateAmdPanelGuide()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };

        stack.Children.Add(CreateGuideBullet(LocalizationService.T(
            "1. Recherche « AMD » dans Windows et ouvre « AMD Software: Adrenalin Edition ».",
            "1. Search for “AMD” in Windows and open “AMD Software: Adrenalin Edition”.")));
        stack.Children.Add(CreateGuideBullet(LocalizationService.T(
            "2. Paramètres (engrenage) → onglet Système → désactive « Détection de problème ».",
            "2. Settings (gear) → System tab → turn off “Issue Detection”.")));
        stack.Children.Add(CreateGuideBullet(LocalizationService.T(
            "3. Onglet Jeux → sélectionne ton jeu → reproduis les réglages Graphismes et Affichage.",
            "3. Gaming tab → select your game → copy the Graphics and Display settings.")));
        stack.Children.Add(CreateGuideBullet(LocalizationService.T(
            "4. Dans la même page Jeux, descends jusqu'à Avancé et applique les valeurs de la capture.",
            "4. On the same Gaming page, scroll down to Advanced and apply the values from the screenshot.")));
        stack.Children.Add(CreateGuideBullet(LocalizationService.T(
            "5. Paramètres → Préférences : désactive superposition, navigateur, notifications, pubs et tutoriels.",
            "5. Settings → Preferences: turn off overlay, browser, notifications, ads and tutorials.")));
        stack.Children.Add(CreateGuideBullet(LocalizationService.T(
            "6. Dans Préférences, désactive aussi animations et programme d'expérience utilisateur AMD.",
            "6. In Preferences, also turn off animations and the AMD user experience program.")));

        var images = new (string File, string CaptionFr, string CaptionEn)[]
        {
            ("amd-panel-01-recherche.png", "Ouvrir AMD Software depuis la recherche Windows", "Open AMD Software from Windows search"),
            ("amd-panel-02-systeme.png", "Paramètres → Système", "Settings → System"),
            ("amd-panel-03-jeux-graphismes.png", "Jeux → Graphismes & Affichage", "Gaming → Graphics & Display"),
            ("amd-panel-04-jeux-avance.png", "Jeux → Réglages avancés", "Gaming → Advanced settings"),
            ("amd-panel-05-preferences-1.png", "Paramètres → Préférences (1/2)", "Settings → Preferences (1/2)"),
            ("amd-panel-06-preferences-2.png", "Paramètres → Préférences (2/2)", "Settings → Preferences (2/2)"),
        };

        for (var i = 0; i < images.Length; i++)
            stack.Children.Add(CreateGuideImage(images[i].File, LocalizationService.T(images[i].CaptionFr, images[i].CaptionEn), i + 1));

        return stack;
    }

    private static UIElement CreateNvidiaPanelGuide()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };

        stack.Children.Add(CreateGuideBullet(LocalizationService.T(
            "1. Paramètres 3D → Régler les paramètres d'image avec Aperçu → coche « Utiliser les paramètres d'image 3D avancés ».",
            "1. 3D Settings → Adjust image settings with preview → check “Use the advanced 3D image settings”.")));
        stack.Children.Add(CreateGuideBullet(LocalizationService.T(
            "2. Clique sur « Afficher cette page » pour ouvrir Gérer les paramètres 3D.",
            "2. Click “Take me there” to open Manage 3D settings.")));
        stack.Children.Add(CreateGuideBullet(LocalizationService.T(
            "3. Dans l'onglet Paramètres globaux, reproduis les valeurs des 3 captures suivantes.",
            "3. In the Global Settings tab, copy the values from the next 3 screenshots.")));

        var images = new (string File, string CaptionFr, string CaptionEn)[]
        {
            ("nvidia-panel-01-apercu.png", "Régler les paramètres d'image avec Aperçu", "Adjust image settings with preview"),
            ("nvidia-panel-02-global-haut.png", "Paramètres globaux — haut de la liste", "Global Settings — top of the list"),
            ("nvidia-panel-03-global-milieu.png", "Paramètres globaux — milieu de la liste", "Global Settings — middle of the list"),
            ("nvidia-panel-04-global-bas.png", "Paramètres globaux — bas de la liste", "Global Settings — bottom of the list"),
        };

        for (var i = 0; i < images.Length; i++)
            stack.Children.Add(CreateGuideImage(images[i].File, LocalizationService.T(images[i].CaptionFr, images[i].CaptionEn), i + 1));

        return stack;
    }

    private static TextBlock CreateGuideBullet(string text) =>
        new()
        {
            Text = "• " + text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            LineHeight = 18,
            Foreground = Brushes.White,
            Opacity = 0.88,
            Margin = new Thickness(0, 0, 0, 8),
        };

    private static Border CreateGuideImage(string fileName, string caption, int index)
    {
        var panel = new StackPanel();

        panel.Children.Add(new TextBlock
        {
            Text = LocalizationService.T($"Capture {index} — {caption}", $"Screenshot {index} — {caption}"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Opacity = 0.92,
            Margin = new Thickness(0, 10, 0, 8),
        });

        var image = new Image
        {
            Source = LoadGuideImage(fileName),
            Stretch = Stretch.Uniform,
            MaxWidth = 720,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        panel.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderBrush = StepBorderBrush,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
            Padding = new Thickness(8),
            Child = image,
            Margin = new Thickness(0, 0, 0, 4),
        });

        return new Border
        {
            Child = panel,
            Margin = new Thickness(0, 0, 0, 6),
        };
    }

    private static Border CreateStepCard(int number, string title, string description, UIElement? body = null)
    {
        var header = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var badge = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(10),
            Background = StepBadgeBrush,
            Margin = new Thickness(0, 0, 12, 0),
            Child = new TextBlock
            {
                Text = number.ToString(),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(badge, 0);
        header.Children.Add(badge);

        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
        });
        titles.Children.Add(new TextBlock
        {
            Text = description,
            Margin = new Thickness(0, 6, 0, 0),
            FontSize = 12,
            LineHeight = 18,
            Foreground = Brushes.White,
            Opacity = 0.82,
            TextWrapping = TextWrapping.Wrap,
        });
        Grid.SetColumn(titles, 1);
        header.Children.Add(titles);

        var content = new StackPanel();
        content.Children.Add(header);
        if (body is not null)
            content.Children.Add(body);

        return new Border
        {
            Padding = new Thickness(16, 14, 16, 14),
            CornerRadius = new CornerRadius(14),
            BorderBrush = StepBorderBrush,
            BorderThickness = new Thickness(1),
            Background = StepBackgroundBrush,
            Margin = new Thickness(0, 0, 0, 12),
            Child = content,
        };
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // ignore
        }
    }

    private static BitmapImage LoadGpuLogo(string fileName)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(AssetHelper.Gpu(fileName), UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private static BitmapImage LoadGuideImage(string fileName)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(AssetHelper.GpuGuide(fileName), UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    public FrameworkElement? GetOnboardingTarget(int step) =>
        SelectionPanel.Visibility == Visibility.Visible ? OnboardingGpuChoices : OnboardingGpuStepsCard;

    public void ScrollToTop() => PageScroll.ScrollToVerticalOffset(0);

    public bool TryEscapeBack()
    {
        if (ResolveVendor() is not (GpuVendor.Nvidia or GpuVendor.Amd))
            return false;

        ShowSelection();
        return true;
    }
}
