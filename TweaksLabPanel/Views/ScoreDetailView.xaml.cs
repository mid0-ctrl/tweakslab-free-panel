using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;
using TweaksLabPanel.Services;

namespace TweaksLabPanel.Views;

/// <summary>Détail transparent du score de santé : chaque critère mesuré, ses points et comment le corriger.</summary>
public partial class ScoreDetailView : UserControl
{
    private static readonly SolidColorBrush AmberBrush = new(Color.FromRgb(0xF5, 0x9E, 0x0B));

    private bool _subscribed;

    static ScoreDetailView()
    {
        AmberBrush.Freeze();
    }

    public ScoreDetailView()
    {
        InitializeComponent();
        OfferText.Text = LocalizationService.T(
            $"Avec l’optimisation complète, ce sera toujours mieux : l’équipe TweaksLab va chercher autour de 100 % des performances de ton PC. Optimisation {OfferLinks.StartingPrice}.",
            $"With the full optimization, it only gets better: the TweaksLab team goes after around 100% of your PC’s performance. Optimization {OfferLinks.StartingPrice}.");

        Loaded += (_, _) =>
        {
            Subscribe();
            Refresh();
        };
        Unloaded += (_, _) => Unsubscribe();
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;

        _subscribed = true;
        PerformanceScoreService.ScoreUpdated += OnScoreChanged;
        PerformanceScoreService.ScanStateChanged += OnScoreChanged;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;

        _subscribed = false;
        PerformanceScoreService.ScoreUpdated -= OnScoreChanged;
        PerformanceScoreService.ScanStateChanged -= OnScoreChanged;
    }

    private void OnScoreChanged() => Dispatcher.BeginInvoke(Refresh);

    private void Refresh()
    {
        var scanning = PerformanceScoreService.IsScanning;
        RescanButton.IsEnabled = !scanning;
        RescanButton.Content = scanning
            ? LocalizationService.T("Analyse en cours…", "Scanning…")
            : LocalizationService.T("Recalculer mon score", "Recalculate my score");

        var breakdown = PerformanceScoreService.Breakdown;
        if (breakdown is null)
        {
            ScoreValueText.Text = "—";
            BandText.Text = LocalizationService.T("Analyse de ton PC en cours…", "Scanning your PC…");
            LastScanText.Text = string.Empty;
            CategoriesHost.Children.Clear();
            return;
        }

        ScoreValueText.Text = breakdown.Score.ToString();
        BandText.Text = PerformanceScoreService.BandLabel(breakdown.Score);

        if (PerformanceScoreService.Snapshot is { } snapshot)
        {
            var local = snapshot.ScannedAtUtc.ToLocalTime();
            var time = local.Date == DateTime.Today
                ? local.ToString("HH:mm")
                : local.ToString(LocalizationService.IsEnglish ? "MM/dd HH:mm" : "dd/MM HH:mm");
            LastScanText.Text = LocalizationService.T($"Dernière analyse : {time}", $"Last scan: {time}");
        }

        BuildCategories(breakdown);
    }

    private void BuildCategories(ScoreBreakdown breakdown)
    {
        CategoriesHost.Children.Clear();
        foreach (var category in Enum.GetValues<ScoreCategory>())
        {
            var criteria = breakdown.Criteria.Where(c => c.Category == category).ToList();
            if (criteria.Count == 0)
                continue;

            var list = new StackPanel();
            list.Children.Add(BuildCategoryHeader(category, breakdown));
            foreach (var criterion in criteria)
                list.Children.Add(BuildCriterionRow(criterion));

            CategoriesHost.Children.Add(new Border
            {
                Style = (Style)FindResource("GlassCard"),
                Padding = new Thickness(20, 14, 20, 10),
                Margin = new Thickness(0, 0, 0, 12),
                Child = list,
            });
        }
    }

    private Grid BuildCategoryHeader(ScoreCategory category, ScoreBreakdown breakdown)
    {
        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        header.Children.Add(new TextBlock
        {
            Text = CategoryLabel(category).ToUpperInvariant(),
            Style = (Style)FindResource("SectionTitle"),
            Margin = new Thickness(0),
        });

        var max = breakdown.MaxPoints(category);
        var points = new TextBlock
        {
            Text = max > 0
                ? LocalizationService.T($"{breakdown.EarnedPoints(category):0.#} / {max} pts", $"{breakdown.EarnedPoints(category):0.#} / {max} pts")
                : "—",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextLightGrayBrush"),
        };
        Grid.SetColumn(points, 1);
        header.Children.Add(points);
        return header;
    }

    private Grid BuildCriterionRow(ScoreCriterion criterion)
    {
        var white = (Brush)FindResource("TextWhiteBrush");
        var gray = (Brush)FindResource("TextGrayBrush");

        var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var (glyph, glyphBrush) = criterion switch
        {
            { Measured: false } => ("", gray),
            { IsPerfect: true } => ("", (Brush)FindResource("GreenAccentBrush")),
            { Earned: > 0 } => ("", AmberBrush),
            _ => ("", (Brush)FindResource("RedAccentBrush")),
        };
        row.Children.Add(new TextBlock
        {
            Text = glyph,
            FontFamily = (FontFamily)FindResource("IconFont"),
            FontSize = 13,
            Foreground = glyphBrush,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
        });

        var texts = new StackPanel();
        texts.Children.Add(new TextBlock
        {
            Text = LocalizationService.T(criterion.LabelFr, criterion.LabelEn),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = criterion.Measured ? white : gray,
            TextWrapping = TextWrapping.Wrap,
        });

        if (criterion.ValueFr is not null)
        {
            texts.Children.Add(new TextBlock
            {
                Text = LocalizationService.T(criterion.ValueFr, criterion.ValueEn ?? criterion.ValueFr),
                FontSize = 11,
                Foreground = gray,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (criterion.TipFr is not null)
        {
            texts.Children.Add(new TextBlock
            {
                Text = LocalizationService.T(criterion.TipFr, criterion.TipEn ?? criterion.TipFr),
                FontSize = 11,
                Foreground = (Brush)FindResource("TextLightGrayBrush"),
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        Grid.SetColumn(texts, 1);
        row.Children.Add(texts);

        var points = new TextBlock
        {
            Text = criterion.Earned is { } earned ? $"{earned:0.#}/{criterion.MaxPoints}" : "—",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = criterion.Measured ? white : gray,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        Grid.SetColumn(points, 2);
        row.Children.Add(points);

        if (criterion.Fix != ScoreFix.None)
        {
            var fix = new TextBlock
            {
                Text = LocalizationService.T("Corriger →", "Fix →"),
                Style = (Style)FindResource("AccentLink"),
                Margin = new Thickness(14, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            fix.MouseLeftButtonUp += (_, e) =>
            {
                ApplyFix(criterion.Fix);
                e.Handled = true;
            };
            Grid.SetColumn(fix, 3);
            row.Children.Add(fix);
        }

        return row;
    }

    private static string CategoryLabel(ScoreCategory category) => category switch
    {
        ScoreCategory.Startup => LocalizationService.T("Démarrage et arrière-plan", "Startup and background"),
        ScoreCategory.Storage => LocalizationService.T("Stockage", "Storage"),
        ScoreCategory.Memory => LocalizationService.T("Mémoire", "Memory"),
        ScoreCategory.Temperatures => LocalizationService.T("Températures", "Temperatures"),
        ScoreCategory.GamingSettings => LocalizationService.T("Réglages gaming Windows", "Windows gaming settings"),
        ScoreCategory.Stability => LocalizationService.T("Stabilité et pilotes", "Stability and drivers"),
        _ => category.ToString(),
    };

    private void ApplyFix(ScoreFix fix)
    {
        var main = Window.GetWindow(this) as MainWindow;
        switch (fix)
        {
            case ScoreFix.TipsPage:
                main?.NavigateTo("Astuces");
                break;
            case ScoreFix.CleanupPage:
                main?.NavigateTo("Nettoyer");
                break;
            case ScoreFix.GpuPage:
                main?.NavigateTo("Gpu");
                break;
            case ScoreFix.DisplaySettingsPage:
                main?.NavigateTo("Parametres");
                break;
            case ScoreFix.XmpTutorial:
                Open(PanelEndpoints.XmpTutorialUrl);
                break;
            case ScoreFix.WindowsGameMode:
                Open("ms-settings:gaming-gamemode");
                break;
            case ScoreFix.WindowsCaptures:
                Open("ms-settings:gaming-gamedvr");
                break;
            case ScoreFix.WindowsGraphics:
                Open("ms-settings:display-advancedgraphics");
                break;
            case ScoreFix.WindowsApps:
                Open("ms-settings:appsfeatures");
                break;
            case ScoreFix.WindowsMouse:
                OpenProcess("control.exe", "main.cpl");
                break;
        }
    }

    private static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch
        {
            // Réglage ou navigateur indisponible.
        }
    }

    private static void OpenProcess(string fileName, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = true });
        }
        catch
        {
            // Indisponible.
        }
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e) => await PerformanceScoreService.ScanAsync();

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow main)
            main.NavigateTo("Accueil");
    }

    private void Offer_Click(object sender, RoutedEventArgs e) => OfferLinks.Open(OfferLinks.ScoreDetail);

    public void ScrollToTop() => PageScroll.ScrollToVerticalOffset(0);
}
