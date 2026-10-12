using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;
using TweaksLabPanel.Services;

namespace TweaksLabPanel.Views;

public partial class HomeView : UserControl
{
    private const double ReviewTickerGap = 56;
    private const double ReviewTickerHeight = 32;
    private const double ReviewMarqueeSpeedPxPerSec = 40;

    private static readonly string[] AppBackgrounds = BackgroundCatalog.FileNames;

    private int _wallIndex;
    private int _bgIndex;
    private bool _wallAnimating;
    private bool _bgAnimating;
    private bool _bgCarouselPaused;
    private bool _wallCarouselPaused;
    private DispatcherTimer? _bgTimer;
    private DispatcherTimer? _wallTimer;
    private bool _ringPlayed;
    private TranslateTransform? _reviewsMarqueeTransform;
    private double _reviewLoopWidth;

    private static readonly IEasingFunction RingEase = new SineEase { EasingMode = EasingMode.EaseOut };
    private static readonly IEasingFunction CarouselEase = new CubicEase { EasingMode = EasingMode.EaseOut };
    private static readonly SolidColorBrush DotIdle = new(Color.FromRgb(0x55, 0x55, 0x55));

    private static Brush DotActive => (Brush)Application.Current.Resources["AccentBrush"];

    public static readonly DependencyProperty RingValueProperty =
        DependencyProperty.Register(nameof(RingValue), typeof(double), typeof(HomeView),
            new PropertyMetadata(0.0, OnRingValueChanged));

    public double RingValue
    {
        get => (double)GetValue(RingValueProperty);
        set => SetValue(RingValueProperty, value);
    }

    private static void OnRingValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HomeView home)
            home.ScoreText.Text = $"{(int)Math.Round((double)e.NewValue)}";
    }

    static HomeView()
    {
        DotIdle.Freeze();
    }

    private readonly Action<LiveSensorReading> _liveSensorsHandler;
    private FeatureActivationState _xmpState = FeatureActivationState.Unknown;
    private bool _subscribed;

    public HomeView()
    {
        InitializeComponent();
        _liveSensorsHandler = reading => Dispatcher.BeginInvoke(() => ApplySensorValues(
            reading.CpuTempC, reading.GpuTempC, reading.CpuUsagePercent, reading.RamUsedGb, reading.RamTotalGb));

        BuildWallpaperDots();
        RefreshWallpaperCarousel();
        BuildBackgroundDots();
        RefreshBackgroundCarousel();

        OfferBannerSubtitle.Text = LocalizationService.T(
            $"Optimisation complète par l’équipe · {OfferLinks.StartingPrice}",
            $"Full optimization by our team · {OfferLinks.StartingPrice}");

        SetDisplayName(UserProfileStore.Instance.ResolvedDisplayName);
        RefreshGreeting();
        ApplyPerformanceSnapshot(PerformanceScoreService.Snapshot);

        WireInfoShowcases();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;

        _subscribed = true;
        PerformanceScoreService.ScoreUpdated += OnPerformanceScoreUpdated;
        LocalizationService.LanguageChanged += OnLanguageChanged;
        LiveSensors.Subscribe(_liveSensorsHandler);
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;

        _subscribed = false;
        PerformanceScoreService.ScoreUpdated -= OnPerformanceScoreUpdated;
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        LiveSensors.Unsubscribe(_liveSensorsHandler);
    }

    private void WireInfoShowcases()
    {
        InfoShowcaseHelper.WireHover(PerformanceInfoBadge, PerformanceInfoShowcase, PerformanceInfoText,
            () => LocalizationService.PerformanceScoreInfo);
        InfoShowcaseHelper.WireHover(ReviewsInfoBadge, ReviewsInfoShowcase, ReviewsInfoText,
            () => LocalizationService.ReviewsInfo);
    }

    private void OnLanguageChanged() =>
        Dispatcher.BeginInvoke(RefreshInfoShowcaseText);

    private void RefreshInfoShowcaseText()
    {
        InfoShowcaseHelper.RefreshText(PerformanceInfoText, () => LocalizationService.PerformanceScoreInfo);
        InfoShowcaseHelper.RefreshText(ReviewsInfoText, () => LocalizationService.ReviewsInfo);
    }

    private void OnPerformanceScoreUpdated()
    {
        Dispatcher.BeginInvoke(() =>
        {
            ApplyPerformanceSnapshot(PerformanceScoreService.Snapshot);
            AnimateRing(PerformanceScoreService.Score);
        });
    }

    public void RefreshOnShow()
    {
        RefreshGreeting();
        ResumeBackgroundCarousel();
        ResumeWallpaperCarousel();
        BuildReviewsMarquee();
        StartReviewsMarquee();
        ApplyPerformanceSnapshot(PerformanceScoreService.Snapshot);
        ScoreRing.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
        BeginAnimation(RingValueProperty, null);
        RingValue = 0;
        ScoreText.Text = "0";
        _ringPlayed = false;
        if (PerformanceScoreService.HasScan)
            AnimateRing(PerformanceScoreService.Score);
    }

    public void SetDisplayName(string displayName)
    {
        WelcomePseudo.Text = string.IsNullOrWhiteSpace(displayName)
            ? UserProfileStore.DefaultDisplayName
            : displayName.Trim();
        RefreshStreak();
    }

    private void RefreshStreak()
    {
        var streak = EngagementStore.Instance.CurrentStreak;
        if (streak <= 1)
        {
            StreakText.Visibility = Visibility.Collapsed;
            return;
        }

        var accentBrush = (Brush)FindResource("AccentBrush");
        var numberBrush = GetStreakNumberBrush(streak);
        var emphasis = FontWeights.SemiBold;

        StreakText.Inlines.Clear();

        if (LocalizationService.IsEnglish)
        {
            StreakText.Inlines.Add(new Run(streak.ToString()) { Foreground = numberBrush, FontWeight = emphasis });
            StreakText.Inlines.Add(new Run(" day streak 🔥") { Foreground = accentBrush, FontWeight = emphasis });
        }
        else
        {
            StreakText.Inlines.Add(new Run("Série de ") { Foreground = accentBrush, FontWeight = emphasis });
            StreakText.Inlines.Add(new Run(streak.ToString()) { Foreground = numberBrush, FontWeight = emphasis });
            StreakText.Inlines.Add(new Run(" jours 🔥") { Foreground = accentBrush, FontWeight = emphasis });
        }

        StreakText.Visibility = Visibility.Visible;
    }

    private Brush GetStreakNumberBrush(int streak)
    {
        if (streak >= 30)
            return (Brush)FindResource("DiamondStreakBrush");

        if (streak >= 10)
            return (Brush)FindResource("GoldAccentBrush");

        return (Brush)FindResource("AccentBrush");
    }

    private void RefreshGreeting()
    {
        WelcomeGreeting.Text = $"{GetTimeGreeting()},";
    }

    private static string GetTimeGreeting()
    {
        var hour = DateTime.Now.Hour;
        return hour switch
        {
            >= 6 and < 19 => LocalizationService.T("Bonjour", "Hello"),
            >= 19 and < 23 => LocalizationService.T("Bonsoir", "Good evening"),
            _ => LocalizationService.T("Bonne nuit", "Good night"),
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Subscribe();
        ApplyPerformanceSnapshot(PerformanceScoreService.Snapshot);

        if (!_ringPlayed && PerformanceScoreService.HasScan)
            AnimateRing(PerformanceScoreService.Score);

        if (!PerformanceScoreService.HasScan)
            _ = PerformanceScoreService.EnsureScannedAsync();

        Dispatcher.BeginInvoke(() =>
        {
            BuildReviewsMarquee();
            StartReviewsMarquee();
        }, DispatcherPriority.Background);

        ResumeWallpaperCarousel();
    }

    public void ResumeBackgroundCarousel()
    {
        _bgTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        _bgTimer.Tick -= AutoAdvanceBackgroundTick;
        _bgTimer.Tick += AutoAdvanceBackgroundTick;
        _bgTimer.Start();
    }

    public void PauseBackgroundCarousel() => _bgTimer?.Stop();

    public void ResumeWallpaperCarousel()
    {
        _wallTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _wallTimer.Tick -= AutoAdvanceWallpaperTick;
        _wallTimer.Tick += AutoAdvanceWallpaperTick;
        _wallTimer.Start();
    }

    public void PauseWallpaperCarousel() => _wallTimer?.Stop();

    private void AutoAdvanceBackgroundTick(object? sender, EventArgs e) => AutoAdvanceBackground();

    private void AutoAdvanceWallpaperTick(object? sender, EventArgs e) => AutoAdvanceWallpaper();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unsubscribe();
        StopReviewsMarquee();

        if (_bgTimer is not null)
        {
            _bgTimer.Stop();
            _bgTimer = null;
        }

        if (_wallTimer is not null)
        {
            _wallTimer.Stop();
            _wallTimer = null;
        }
    }

    private void WpCarousel_MouseEnter(object sender, MouseEventArgs e) => _wallCarouselPaused = true;

    private void WpCarousel_MouseLeave(object sender, MouseEventArgs e) => _wallCarouselPaused = false;

    private void BgCarousel_MouseEnter(object sender, MouseEventArgs e) => _bgCarouselPaused = true;

    private void BgCarousel_MouseLeave(object sender, MouseEventArgs e) => _bgCarouselPaused = false;

    private void AutoAdvanceBackground()
    {
        if (_bgCarouselPaused || _bgAnimating) return;
        _bgIndex = (_bgIndex + 1) % AppBackgrounds.Length;
        RefreshBackgroundCarousel();
        AnimateBackgroundSwap(1);
    }

    private void AutoAdvanceWallpaper()
    {
        if (_wallCarouselPaused || _wallAnimating) return;
        _wallIndex = (_wallIndex + 1) % WallpaperCatalog.Featured.Length;
        RefreshWallpaperCarousel();
        AnimateWallpaperSwap(1);
    }

    private void AnimateRing(int score)
    {
        score = Math.Clamp(score, PerformanceScoreDefaults.MinScore, PerformanceScoreDefaults.MaxScore);
        ScoreCaptionText.Text = PerformanceScoreService.BandLabel(score);

        // Anneau sur 100 : même à 90/90 il ne se ferme pas, la perfection n'existe pas.
        var size = ScoreRing.ActualWidth > 0 ? ScoreRing.ActualWidth : 108.0;
        var radius = size / 2.0;
        var thickness = ScoreRing.StrokeThickness;
        var dashUnit = (2 * Math.PI * radius) / thickness;
        var fraction = score / 100.0;
        var duration = TimeSpan.FromMilliseconds(1200);

        if (!_ringPlayed)
        {
            // Première apparition : l'anneau se remplit depuis zéro.
            ScoreRing.StrokeDashArray = new DoubleCollection { dashUnit, dashUnit };
            ScoreRing.StrokeDashOffset = dashUnit;
            ScoreRing.BeginAnimation(Shape.StrokeDashOffsetProperty,
                new DoubleAnimation(dashUnit, dashUnit * (1 - fraction), duration)
                {
                    EasingFunction = RingEase,
                    FillBehavior = FillBehavior.HoldEnd,
                });
            BeginAnimation(RingValueProperty,
                new DoubleAnimation(0, score, duration)
                {
                    EasingFunction = RingEase,
                    FillBehavior = FillBehavior.HoldEnd,
                });
            _ringPlayed = true;
            return;
        }

        // Nouveau scan ou bonus : l'anneau glisse de la valeur affichée vers la nouvelle.
        ScoreRing.BeginAnimation(Shape.StrokeDashOffsetProperty,
            new DoubleAnimation(dashUnit * (1 - fraction), duration)
            {
                EasingFunction = RingEase,
                FillBehavior = FillBehavior.HoldEnd,
            });
        BeginAnimation(RingValueProperty,
            new DoubleAnimation(score, duration)
            {
                EasingFunction = RingEase,
                FillBehavior = FillBehavior.HoldEnd,
            });
    }

    private void BuildWallpaperDots()
    {
        WpDots.Children.Clear();
        for (var i = 0; i < WallpaperCatalog.Featured.Length; i++)
        {
            WpDots.Children.Add(new Ellipse
            {
                Width = i == _wallIndex ? 7 : 5,
                Height = i == _wallIndex ? 7 : 5,
                Margin = new Thickness(3),
                Fill = i == _wallIndex ? DotActive : DotIdle,
            });
        }
    }

    private void RefreshWallpaperCarousel()
    {
        var count = WallpaperCatalog.Featured.Length;
        if (count == 0) return;

        var left = ((_wallIndex - 1) % count + count) % count;
        var right = (_wallIndex + 1) % count;

        WpLeft.Source = WallpaperService.LoadFeatured(left);
        WpCenter.Source = WallpaperService.LoadFeatured(_wallIndex);
        WpRight.Source = WallpaperService.LoadFeatured(right);

        for (var i = 0; i < WpDots.Children.Count; i++)
        {
            if (WpDots.Children[i] is not Ellipse dot) continue;
            var active = i == _wallIndex;
            dot.Width = active ? 7 : 5;
            dot.Height = active ? 7 : 5;
            dot.Fill = active ? DotActive : DotIdle;
        }
    }

    private void BuildBackgroundDots()
    {
        BgDots.Children.Clear();
        for (var i = 0; i < AppBackgrounds.Length; i++)
        {
            BgDots.Children.Add(new Ellipse
            {
                Width = i == _bgIndex ? 5 : 4,
                Height = i == _bgIndex ? 5 : 4,
                Margin = new Thickness(2),
                Fill = i == _bgIndex ? DotActive : DotIdle,
            });
        }
    }

    private void RefreshBackgroundCarousel()
    {
        var count = AppBackgrounds.Length;
        if (count == 0) return;

        var left = ((_bgIndex - 1) % count + count) % count;
        var right = (_bgIndex + 1) % count;

        BgLeft.Source = LoadAppBackground(left);
        BgCenter.Source = LoadAppBackground(_bgIndex);
        BgRight.Source = LoadAppBackground(right);

        BackgroundMotionHelper.Attach(BgLeft, BackgroundMotionProfile.Preview);
        BackgroundMotionHelper.Attach(BgCenter, BackgroundMotionProfile.Preview);
        BackgroundMotionHelper.Attach(BgRight, BackgroundMotionProfile.Preview);

        for (var i = 0; i < BgDots.Children.Count; i++)
        {
            if (BgDots.Children[i] is not Ellipse dot) continue;
            var active = i == _bgIndex;
            dot.Width = active ? 5 : 4;
            dot.Height = active ? 5 : 4;
            dot.Fill = active ? DotActive : DotIdle;
        }
    }

    private static BitmapImage LoadAppBackground(int index) =>
        new(new Uri(AssetHelper.Background(AppBackgrounds[index])));

    private void BgCarousel_Click(object sender, MouseButtonEventArgs e)
    {
        GoToBackgroundLibrary();
        e.Handled = true;
    }

    private void AnimateBackgroundSwap(int direction)
    {
        _bgAnimating = true;
        var duration = TimeSpan.FromMilliseconds(280);
        var ease = CarouselEase;

        BgCenterFrame.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0.35, 1, duration) { EasingFunction = ease });

        BgCenterTranslate.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(direction * 28, 0, duration) { EasingFunction = ease });

        BgCenterScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.94, 1, duration) { EasingFunction = ease });
        BgCenterScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.94, 1, duration) { EasingFunction = ease });

        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _bgAnimating = false;
        };
        timer.Start();
    }

    private void AnimateWallpaperSwap(int direction)
    {
        _wallAnimating = true;
        var duration = TimeSpan.FromMilliseconds(280);
        var ease = CarouselEase;

        WpCenterFrame.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0.35, 1, duration) { EasingFunction = ease });

        WpCenterTranslate.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(direction * 60, 0, duration) { EasingFunction = ease });

        WpCenterScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.94, 1, duration) { EasingFunction = ease });
        WpCenterScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.94, 1, duration) { EasingFunction = ease });

        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _wallAnimating = false;
        };
        timer.Start();
    }

    private void BuildReviewsMarquee()
    {
        ReviewsTrackCanvas.Children.Clear();
        _reviewLoopWidth = 0;

        if (ReviewCatalog.All.Length == 0)
            return;

        double x = 0;
        void AppendSet()
        {
            foreach (var review in ReviewCatalog.All)
            {
                var chip = BuildReviewTickerChip(review);
                chip.Measure(new Size(double.PositiveInfinity, ReviewTickerHeight));
                chip.Arrange(new Rect(0, 0, chip.DesiredSize.Width, chip.DesiredSize.Height));
                var w = Math.Max(chip.DesiredSize.Width, 1);
                var h = Math.Max(chip.DesiredSize.Height, 1);

                Canvas.SetLeft(chip, x);
                Canvas.SetTop(chip, Math.Max(0, (ReviewTickerHeight - h) / 2));
                ReviewsTrackCanvas.Children.Add(chip);
                x += w + ReviewTickerGap;
            }
        }

        AppendSet();
        _reviewLoopWidth = x;
        AppendSet();
        ReviewsTrackCanvas.Width = x;
    }

    private int _reviewsMarqueeRetries;

    private void StartReviewsMarquee()
    {
        if (_reviewLoopWidth <= 0)
        {
            if (++_reviewsMarqueeRetries > 24)
                return;

            Dispatcher.BeginInvoke(StartReviewsMarquee, DispatcherPriority.ApplicationIdle);
            return;
        }

        _reviewsMarqueeRetries = 0;
        _reviewsMarqueeTransform ??= new TranslateTransform();
        ReviewsTrackCanvas.RenderTransform = _reviewsMarqueeTransform;

        var seconds = _reviewLoopWidth / ReviewMarqueeSpeedPxPerSec;
        _reviewsMarqueeTransform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(0, -_reviewLoopWidth, TimeSpan.FromSeconds(seconds))
            {
                RepeatBehavior = RepeatBehavior.Forever,
            });
    }

    private void StopReviewsMarquee()
    {
        _reviewsMarqueeTransform?.BeginAnimation(TranslateTransform.XProperty, null);
    }

    private Border BuildReviewTickerChip(ReviewCatalog.Review review)
    {
        var chip = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 4, 14, 4),
            Background = new SolidColorBrush(Color.FromArgb(0x44, 0x1A, 0x15, 0x26)),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true,
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        row.Children.Add(new Border
        {
            Width = 1,
            Height = 14,
            Margin = new Thickness(0, 0, 12, 0),
            Background = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
            VerticalAlignment = VerticalAlignment.Center,
        });

        row.Children.Add(new TextBlock
        {
            Text = "★★★★★",
            FontSize = 8,
            Foreground = (Brush)FindResource("GoldAccentBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        });

        row.Children.Add(new TextBlock
        {
            Text = review.Quote,
            FontSize = 10,
            Foreground = (Brush)FindResource("TextGrayBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            TextWrapping = TextWrapping.NoWrap,
        });

        var avatarHost = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(review.Accent)!),
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock
            {
                Text = review.Initial,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        row.Children.Add(avatarHost);

        row.Children.Add(new TextBlock
        {
            Text = review.User,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextWhiteBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        });

        row.Children.Add(new TextBlock
        {
            Text = review.Date,
            FontSize = 9,
            Foreground = (Brush)FindResource("TextDimBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        });

        chip.Child = row;
        return chip;
    }

    private void ApplyPerformanceSnapshot(PerformanceSnapshot? snapshot)
    {
        if (snapshot is null)
            return;

        // Les relevés en direct priment ; le scan sert d'affichage initial.
        if (LiveSensors.Latest is null)
            ApplySensorValues(snapshot.CpuTempC, snapshot.GpuTempC, null, snapshot.RamUsedGb, snapshot.RamTotalGb);

        ApplyXmpState(snapshot.Xmp);
    }

    private static string NotAvailableText => LocalizationService.T("N/D", "N/A");

    private void ApplySensorValues(double? cpuTempC, double? gpuTempC, double? cpuUsagePercent, double ramUsedGb, double ramTotalGb)
    {
        var white = (Brush)FindResource("TextWhiteBrush");
        var gray = (Brush)FindResource("TextGrayBrush");

        if (cpuTempC is { } cpuTemp)
        {
            var approximate = HardwareSensors.CpuTemperatureIsApproximate;
            CpuLabelText.Text = "CPU";
            CpuTempText.Text = approximate ? $"~{cpuTemp:0}°C" : $"{cpuTemp:0}°C";
            CpuTempText.Foreground = white;
            CpuTempText.ToolTip = approximate
                ? LocalizationService.T(
                    "Mesure du capteur de la carte mère, proche de la température du processeur.",
                    "Reading from the motherboard sensor, close to the CPU temperature.")
                : null;
        }
        else if (cpuUsagePercent is { } cpuUsage)
        {
            CpuLabelText.Text = LocalizationService.T("CPU (charge)", "CPU (load)");
            CpuTempText.Text = $"{cpuUsage:0} %";
            CpuTempText.Foreground = white;
            CpuTempText.ToolTip = LocalizationService.T(
                "Windows ne donne pas la température du processeur sur ce PC : la charge actuelle est affichée à la place.",
                "Windows doesn't expose the CPU temperature on this PC: the current load is shown instead.");
        }
        else
        {
            CpuLabelText.Text = "CPU";
            CpuTempText.Text = NotAvailableText;
            CpuTempText.Foreground = gray;
            CpuTempText.ToolTip = LocalizationService.T(
                "Température du processeur non accessible sur ce PC.",
                "CPU temperature isn't available on this PC.");
        }

        if (gpuTempC is { } gpuTemp)
        {
            GpuTempText.Text = $"{gpuTemp:0}°C";
            GpuTempText.Foreground = white;
            GpuTempText.ToolTip = null;
        }
        else
        {
            GpuTempText.Text = NotAvailableText;
            GpuTempText.Foreground = gray;
            GpuTempText.ToolTip = LocalizationService.T(
                "La carte graphique n'expose pas sa température à Windows (souvent le cas des puces intégrées).",
                "The graphics card doesn't report its temperature to Windows (common on integrated chips).");
        }

        RamUsageText.Text = ramTotalGb > 0
            ? $"{ramUsedGb:0.#} / {ramTotalGb:0.#} GB"
            : "—";
    }

    private void ApplyXmpState(FeatureActivationState state)
    {
        _xmpState = state;
        var disabled = state == FeatureActivationState.Disabled;

        XmpStatusText.Text = state switch
        {
            FeatureActivationState.Enabled => LocalizationService.T("Activé", "Enabled"),
            FeatureActivationState.Disabled => LocalizationService.T("Désactivé", "Disabled"),
            FeatureActivationState.NotSupported => LocalizationService.T("Non dispo", "N/A"),
            _ => LocalizationService.T("Inconnu", "Unknown"),
        };

        XmpStatusText.Foreground = state switch
        {
            FeatureActivationState.Enabled => (Brush)FindResource("GreenAccentBrush"),
            FeatureActivationState.Disabled => (Brush)FindResource("RedAccentBrush"),
            _ => (Brush)FindResource("TextGrayBrush"),
        };

        XmpStatusText.Cursor = disabled ? Cursors.Hand : null;
        XmpStatusText.TextDecorations = disabled ? TextDecorations.Underline : null;
        XmpStatusText.ToolTip = state switch
        {
            FeatureActivationState.Disabled => LocalizationService.T(
                "Ta RAM tourne moins vite que sa vitesse prévue. Clique pour voir comment activer XMP / EXPO dans le BIOS.",
                "Your RAM runs slower than its rated speed. Click to see how to enable XMP / EXPO in the BIOS."),
            FeatureActivationState.NotSupported => LocalizationService.T(
                "Pas de profil XMP / EXPO à activer sur ce PC (souvent le cas des portables).",
                "No XMP / EXPO profile to enable on this PC (common on laptops)."),
            FeatureActivationState.Unknown => LocalizationService.T(
                "Impossible de savoir si XMP / EXPO est actif sur ce PC.",
                "Can't tell whether XMP / EXPO is active on this PC."),
            _ => null,
        };
    }

    private void XmpStatus_Click(object sender, MouseButtonEventArgs e)
    {
        if (_xmpState != FeatureActivationState.Disabled)
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(PanelEndpoints.XmpTutorialUrl) { UseShellExecute = true });
        }
        catch
        {
            // Navigateur indisponible.
        }

        e.Handled = true;
    }

    private void GoToProfil()
    {
        if (Window.GetWindow(this) is MainWindow main)
            main.NavigateToProfilPersonalization();
    }

    private void WallpaperBubble_Click(object sender, MouseButtonEventArgs e)
    {
        GoToWallpaperLibrary();
        e.Handled = true;
    }

    private void GoToWallpaperLibrary()
    {
        if (Window.GetWindow(this) is MainWindow main)
            main.NavigateToWallpaperLibrary(returnTo: "Accueil", scrollToBottom: true);
    }

    private void DiscussTeam_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow main)
            main.NavigateTo("Assistance");
    }

    private void GoToBackgroundLibrary()
    {
        if (Window.GetWindow(this) is MainWindow main)
            main.NavigateToBackgroundLibrary(returnTo: "Accueil");
    }

    private void PersonalizeBackground_Click(object sender, MouseButtonEventArgs e) => GoToBackgroundLibrary();

    private void OfferBanner_Click(object sender, MouseButtonEventArgs e)
    {
        OfferLinks.Open(OfferLinks.HomeBanner);
        e.Handled = true;
    }

    private void ScoreRing_Click(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow main)
            main.NavigateTo("Score");
        e.Handled = true;
    }

    public FrameworkElement? GetOnboardingTarget(int step) => step switch
    {
        0 => OnboardingWelcomeHeader,
        1 => OnboardingPerformance,
        _ => null,
    };
}
