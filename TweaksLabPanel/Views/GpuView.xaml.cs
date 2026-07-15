using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;

namespace TweaksLabPanel.Views;

public partial class GpuView : UserControl
{
    private static readonly SolidColorBrush StepBorderBrush = new(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush StepBackgroundBrush = new(Color.FromArgb(0x1A, 0x14, 0x10, 0x22));
    private static readonly SolidColorBrush StepBadgeBrush = new(Color.FromArgb(0x44, 0xA8, 0x55, 0xF7));

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
        Loaded += (_, _) => RefreshViewState();
    }

    private void NvidiaCard_Click(object sender, MouseButtonEventArgs e) => SelectVendor(GpuVendor.Nvidia);

    private void AmdCard_Click(object sender, MouseButtonEventArgs e) => SelectVendor(GpuVendor.Amd);

    private void ChangeVendor_Click(object sender, RoutedEventArgs e)
    {
        GpuVendorStore.Instance.Clear();
        RefreshViewState();
    }

    private void SelectVendor(string vendor)
    {
        GpuVendorStore.Instance.Save(vendor);
        RefreshViewState();
    }

    private void RefreshViewState()
    {
        var vendor = GpuVendorStore.Instance.SelectedVendor;
        var hasSelection = vendor is GpuVendor.Nvidia or GpuVendor.Amd;

        SelectionPanel.Visibility = hasSelection ? Visibility.Collapsed : Visibility.Visible;
        StepsPanel.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;

        if (!hasSelection)
            return;

        SelectedVendorLogo.Source = LoadGpuLogo(vendor == GpuVendor.Nvidia ? "nvidia.png" : "amd.png");
        SelectedVendorTitle.Text = vendor == GpuVendor.Nvidia ? "NVIDIA" : "AMD";
        BuildSteps(vendor!);
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
            "Réinstaller ses pilotes (DDU + Install)",
            "Pour une installation propre, désinstalle d'abord tes anciens pilotes avec DDU, puis réinstalle la dernière version depuis le site NVIDIA.",
            CreateDduVideoSection(PanelEndpoints.GpuNvidiaDduVideoUrl)));

        StepsHost.Children.Add(CreateStepCard(
            2,
            "Optimiser le Panneau de configuration NVIDIA",
            "Suis les captures dans l'ordre. Ouvre le panneau NVIDIA, puis applique les mêmes réglages dans Paramètres 3D → Paramètres globaux.",
            CreateNvidiaPanelGuide()));
    }

    private void BuildAmdSteps()
    {
        StepsHost.Children.Add(CreateStepCard(
            1,
            "Réinstaller ses pilotes (DDU + Install)",
            "Pour une installation propre, désinstalle d'abord tes anciens pilotes avec DDU, puis réinstalle la dernière version depuis le site AMD.",
            CreateDduVideoSection(PanelEndpoints.GpuAmdDduVideoUrl)));

        StepsHost.Children.Add(CreateStepCard(
            2,
            "Optimiser AMD Software : Adrenalin Edition",
            "Suis les captures dans l'ordre. Ouvre AMD Software, puis reproduis les mêmes réglages dans chaque section.",
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
            Content = "Voir le tutoriel YouTube",
        };

        if (hasUrl)
            button.Click += (_, _) => OpenUrl(videoUrl);

        stack.Children.Add(button);

        if (!hasUrl)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Le lien vidéo sera ajouté prochainement.",
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

        stack.Children.Add(CreateGuideBullet(
            "1. Recherche « AMD » dans Windows et ouvre « AMD Software: Adrenalin Edition »."));
        stack.Children.Add(CreateGuideBullet(
            "2. Paramètres (engrenage) → onglet Système → désactive « Détection de problème »."));
        stack.Children.Add(CreateGuideBullet(
            "3. Onglet Jeux → sélectionne ton jeu → reproduis les réglages Graphismes et Affichage."));
        stack.Children.Add(CreateGuideBullet(
            "4. Dans la même page Jeux, descends jusqu'à Avancé et applique les valeurs de la capture."));
        stack.Children.Add(CreateGuideBullet(
            "5. Paramètres → Préférences : désactive superposition, navigateur, notifications, pubs et tutoriels."));
        stack.Children.Add(CreateGuideBullet(
            "6. Dans Préférences, désactive aussi animations et programme d'expérience utilisateur AMD."));

        var images = new (string File, string Caption)[]
        {
            ("amd-panel-01-recherche.png", "Ouvrir AMD Software depuis la recherche Windows"),
            ("amd-panel-02-systeme.png", "Paramètres → Système"),
            ("amd-panel-03-jeux-graphismes.png", "Jeux → Graphismes & Affichage"),
            ("amd-panel-04-jeux-avance.png", "Jeux → Réglages avancés"),
            ("amd-panel-05-preferences-1.png", "Paramètres → Préférences (1/2)"),
            ("amd-panel-06-preferences-2.png", "Paramètres → Préférences (2/2)"),
        };

        for (var i = 0; i < images.Length; i++)
            stack.Children.Add(CreateGuideImage(images[i].File, images[i].Caption, i + 1));

        return stack;
    }

    private static UIElement CreateNvidiaPanelGuide()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };

        stack.Children.Add(CreateGuideBullet(
            "1. Paramètres 3D → Régler les paramètres d'image avec Aperçu → coche « Utiliser les paramètres d'image 3D avancés »."));
        stack.Children.Add(CreateGuideBullet(
            "2. Clique sur « Afficher cette page » pour ouvrir Gérer les paramètres 3D."));
        stack.Children.Add(CreateGuideBullet(
            "3. Dans l'onglet Paramètres globaux, reproduis les valeurs des 3 captures suivantes."));

        var images = new (string File, string Caption)[]
        {
            ("nvidia-panel-01-apercu.png", "Régler les paramètres d'image avec Aperçu"),
            ("nvidia-panel-02-global-haut.png", "Paramètres globaux — haut de la liste"),
            ("nvidia-panel-03-global-milieu.png", "Paramètres globaux — milieu de la liste"),
            ("nvidia-panel-04-global-bas.png", "Paramètres globaux — bas de la liste"),
        };

        for (var i = 0; i < images.Length; i++)
            stack.Children.Add(CreateGuideImage(images[i].File, images[i].Caption, i + 1));

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
            Text = $"Capture {index} — {caption}",
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
        var vendor = GpuVendorStore.Instance.SelectedVendor;
        if (vendor is not (GpuVendor.Nvidia or GpuVendor.Amd))
            return false;

        GpuVendorStore.Instance.Clear();
        RefreshViewState();
        ScrollToTop();
        return true;
    }
}
