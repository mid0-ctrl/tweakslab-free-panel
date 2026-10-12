using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TweaksLabPanel.Core;
using TweaksLabPanel.Helpers;
using TweaksLabPanel.Services;

namespace TweaksLabPanel.Views;

public partial class AssistanceView : UserControl
{
    private const string BotName = "Bryan";

    private static readonly Regex UrlRegex = new(
        @"https?://[^\s]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, string> GameNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fortnite"] = "Fortnite",
        ["valorant"] = "Valorant",
        ["csgo"] = "Counter-Strike",
    };

    // Seule la config matérielle est mise en cache ; score, XMP et langue sont relus à chaque message.
    private static SystemSpecsSnapshot? _specsCache;

    private readonly List<ChatMessage> _history = [];
    private readonly DispatcherTimer _typingTimer = new() { Interval = TimeSpan.FromMilliseconds(380) };
    private readonly List<Ellipse> _typingDots = [];
    private int _typingPhase;
    private bool _sending;
    private UIElement? _typingRow;
    private UIElement? _suggestionsRow;

    public AssistanceView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshUserIdentity();
        BotAvatar.Source = LoadBotAvatar();
        RefreshUserIdentity();
        _typingTimer.Tick += (_, _) => AnimateTypingDots();

        ChatInput.MaxLength = ChatService.MaxMessageLength;
        ChatInput.KeyDown += ChatInput_KeyDown;
        OfferButton.Content = LocalizationService.T(
            $"Voir l’optimisation · {OfferLinks.StartingPrice}",
            $"See the optimization · {OfferLinks.StartingPrice}");

        AddBotMessage(BuildWelcomeMessage());
        ShowSuggestions();
        UpdateCharCounter();
        UpdateQuotaStatus();
    }

    /// <summary>Une conversation est en cours : la page est conservée au changement de langue.</summary>
    public bool HasConversation => _history.Count > 0;

    private static ImageSource LoadBotAvatar() =>
        new BitmapImage(new Uri(AssetHelper.AssistantBryan, UriKind.Absolute));

    private void RefreshUserIdentity()
    {
        var profile = UserProfileStore.Instance;
        UserAvatar.Source = profile.ResolveAvatarSource();
    }

    private static string BuildWelcomeMessage()
    {
        var profile = UserProfileStore.Instance;
        var name = profile.HasDisplayName ? " " + profile.DisplayName : string.Empty;
        var score = PerformanceScoreService.HasScan ? PerformanceScoreService.Score : (int?)null;
        var xmpDisabled = PerformanceScoreService.Snapshot?.Xmp == FeatureActivationState.Disabled;

        var fr = $"Salut{name} ! Je suis Bryan, ton assistant PC.";
        var en = $"Hi{name}! I’m Bryan, your PC assistant.";

        if (score is { } value)
        {
            fr += $" Ton score de santé est de {value}/90.";
            en += $" Your health score is {value}/90.";
        }

        if (xmpDisabled)
        {
            fr += " J’ai aussi vu que ton XMP / EXPO est désactivé : ta RAM ne tourne pas à sa vitesse prévue.";
            en += " I also noticed your XMP / EXPO is disabled: your RAM isn’t running at its rated speed.";
        }

        fr += " Pose-moi une question sur l’optimisation, les performances ou le dépannage.";
        en += " Ask me anything about optimization, performance or troubleshooting.";
        return LocalizationService.T(fr, en);
    }

    private static List<string> BuildSuggestions()
    {
        var suggestions = new List<string>();

        if (PerformanceScoreService.Snapshot?.Xmp == FeatureActivationState.Disabled)
            suggestions.Add(LocalizationService.T("Mon XMP est désactivé, c’est grave ?", "My XMP is disabled, is that bad?"));

        if (PerformanceScoreService.HasScan)
        {
            var score = PerformanceScoreService.Score;
            suggestions.Add(LocalizationService.T($"Comment améliorer mon score de {score}/90 ?", $"How can I improve my {score}/90 score?"));
        }

        var game = UserProfileStore.Instance.FavoriteGames
            .Select(g => GameNames.TryGetValue(g, out var display) ? display : null)
            .FirstOrDefault(g => g is not null);
        suggestions.Add(game is null
            ? LocalizationService.T("Comment gagner des FPS en jeu ?", "How do I get more FPS in games?")
            : LocalizationService.T($"Comment gagner des FPS sur {game} ?", $"How do I get more FPS in {game}?"));

        suggestions.Add(LocalizationService.T("C’est quoi l’optimisation TweaksLab ?", "What is the TweaksLab optimization?"));
        return suggestions.Take(4).ToList();
    }

    /// <summary>Infos réelles du PC envoyées au bot en début de conversation (pas de pseudo ni de donnée perso).</summary>
    private static async Task<string?> BuildPcContextAsync()
    {
        try
        {
            var specs = _specsCache ??= await Task.Run(SystemSpecsScanner.Scan);
            var snapshot = PerformanceScoreService.Snapshot;
            var parts = new List<string>
            {
                $"Score de santé du panel : {PerformanceScoreService.Score}/90 (calculé sur des mesures réelles du PC)",
                $"CPU : {specs.Processor} ({specs.ProcessorDetails})",
                $"GPU : {specs.GraphicsCard}",
                $"RAM : {specs.Memory}",
                $"Windows : {specs.OperatingSystem}",
            };

            if (snapshot is not null)
            {
                parts.Add(snapshot.Xmp switch
                {
                    FeatureActivationState.Enabled => "XMP/EXPO : activé",
                    FeatureActivationState.Disabled => "XMP/EXPO : désactivé (RAM sous sa vitesse prévue)",
                    FeatureActivationState.NotSupported => "XMP/EXPO : non disponible (portable)",
                    _ => "XMP/EXPO : inconnu",
                });
                parts.Add($"Applications au démarrage : {snapshot.StartupAppsCount}");
                if (snapshot.CpuTempC is { } cpuTemp)
                    parts.Add($"Température CPU : {cpuTemp:0} °C");
                if (snapshot.GpuTempC is { } gpuTemp)
                    parts.Add($"Température GPU : {gpuTemp:0} °C");
            }

            // Points faibles réellement mesurés : Bryan peut conseiller précisément.
            var weakPoints = PerformanceScoreService.Breakdown?.Criteria
                .Where(c => c.Measured && !c.IsPerfect)
                .Select(c => $"{c.LabelFr} ({c.ValueFr})")
                .ToList() ?? [];
            if (weakPoints.Count > 0)
                parts.Add($"Points à améliorer mesurés : {string.Join(", ", weakPoints)}");

            var games = UserProfileStore.Instance.FavoriteGames
                .Select(g => GameNames.TryGetValue(g, out var display) ? display : g)
                .ToList();
            if (games.Count > 0)
                parts.Add($"Jeux favoris : {string.Join(", ", games)}");

            parts.Add(LocalizationService.IsEnglish ? "Langue du panel : anglais (réponds en anglais)" : "Langue du panel : français");

            return "[Infos envoyées automatiquement par le panel TweaksLab pour personnaliser tes conseils] "
                + string.Join(" · ", parts);
        }
        catch
        {
            return null;
        }
    }

    private void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            Send_Click(sender, e);
            e.Handled = true;
        }
    }

    private void ChatInput_TextChanged(object sender, TextChangedEventArgs e) => UpdateCharCounter();

    private void UpdateCharCounter() =>
        CharCounterText.Text = $"{ChatInput.Text.Length}/{ChatService.MaxMessageLength}";

    private void UpdateQuotaStatus()
    {
        var remaining = ChatQuotaStore.Instance.RemainingToday;
        StatusText.Text = remaining switch
        {
            0 => LocalizationService.T(
                $"Limite de {ChatQuotaStore.DailyLimit} messages atteinte pour aujourd’hui.",
                $"Daily limit of {ChatQuotaStore.DailyLimit} messages reached."),
            <= 5 => LocalizationService.T(
                $"{remaining} message(s) restant(s) aujourd’hui.",
                $"{remaining} message(s) left today."),
            _ => string.Empty,
        };

        var blocked = remaining == 0;
        ChatInput.IsEnabled = !blocked && !_sending;
        SendButton.IsEnabled = !blocked && !_sending;
    }

    private async void Send_Click(object sender, RoutedEventArgs e) => await SendMessageAsync(ChatInput.Text);

    private async Task SendMessageAsync(string raw)
    {
        if (_sending)
            return;

        var text = raw.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text.Length > ChatService.MaxMessageLength)
            text = text[..ChatService.MaxMessageLength];

        var cooldown = ChatQuotaStore.Instance.CooldownRemaining;
        if (cooldown > TimeSpan.Zero)
        {
            StatusText.Text = LocalizationService.T(
                $"Patiente {Math.Ceiling(cooldown.TotalSeconds)} s avant ton prochain message.",
                $"Wait {Math.Ceiling(cooldown.TotalSeconds)} s before your next message.");
            return;
        }

        if (!ChatQuotaStore.Instance.TryConsume())
        {
            AddBotMessage(
                LocalizationService.T(
                    $"Tu as atteint la limite de {ChatQuotaStore.DailyLimit} messages pour aujourd’hui. Reviens demain, ou passe sur le Discord pour parler directement à l’équipe.",
                    $"You’ve reached today’s limit of {ChatQuotaStore.DailyLimit} messages. Come back tomorrow, or hop on Discord to talk to the team directly."),
                new ChatCta(LocalizationService.T("Rejoindre le Discord", "Join the Discord"), PanelEndpoints.SocialDiscord));
            UpdateQuotaStatus();
            return;
        }

        RemoveSuggestions();
        RefreshUserIdentity();
        ChatInput.Text = string.Empty;
        AddUserMessage(text);
        _history.Add(new ChatMessage("user", text));

        _sending = true;
        SendButton.IsEnabled = false;
        ChatInput.IsEnabled = false;
        StatusText.Text = string.Empty;
        ShowTypingIndicator();

        try
        {
            var context = await BuildPcContextAsync();
            var outcome = await ChatService.SendAsync(text, _history.Take(_history.Count - 1).ToList(), context);
            RemoveTypingIndicator();

            if (outcome.Result is not { } result)
            {
                // Message sans réponse : on ne le renvoie pas dans l'historique et il ne compte pas dans le quota.
                _history.RemoveAt(_history.Count - 1);
                ChatQuotaStore.Instance.Refund();
                AddBotMessage(outcome.Failure == ChatFailure.RateLimited
                    ? LocalizationService.T(
                        "Beaucoup de messages en peu de temps : patiente un peu avant de réessayer, ou passe sur le Discord pour parler à l’équipe.",
                        "Lots of messages in a short time: wait a bit before trying again, or hop on Discord to talk to the team.")
                    : LocalizationService.T(
                        "Je n’ai pas pu joindre l’assistant pour le moment. Réessaie dans un instant.",
                        "I couldn’t reach the assistant right now. Try again in a moment."));
                return;
            }

            _history.Add(new ChatMessage("assistant", result.Reply));
            AddBotMessage(result.Reply, result.Cta, result.TicketCta);
        }
        catch
        {
            RemoveTypingIndicator();
            if (_history.Count > 0 && _history[^1].Role == "user")
                _history.RemoveAt(_history.Count - 1);
            ChatQuotaStore.Instance.Refund();
            AddBotMessage(LocalizationService.T(
                "Je n’ai pas pu joindre l’assistant pour le moment. Réessaie dans un instant.",
                "I couldn’t reach the assistant right now. Try again in a moment."));
        }
        finally
        {
            _sending = false;
            UpdateQuotaStatus();
            if (ChatInput.IsEnabled)
                ChatInput.Focus();
        }
    }

    private void ShowSuggestions()
    {
        var panel = new WrapPanel { Margin = new Thickness(56, 0, 0, 8) };
        foreach (var suggestion in BuildSuggestions())
        {
            var chip = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0x7C, 0x3A, 0xED)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xA8, 0x55, 0xF7)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 8, 8),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = suggestion,
                    FontSize = 12,
                    Foreground = (Brush)FindResource("TextWhiteBrush"),
                },
            };
            chip.MouseLeftButtonUp += async (_, _) => await SendMessageAsync(suggestion);
            panel.Children.Add(chip);
        }

        _suggestionsRow = panel;
        ChatMessages.Children.Add(panel);
        ScrollToEnd();
    }

    private void RemoveSuggestions()
    {
        if (_suggestionsRow is null)
            return;

        ChatMessages.Children.Remove(_suggestionsRow);
        _suggestionsRow = null;
    }

    private void OfferButton_Click(object sender, RoutedEventArgs e) => OfferLinks.Open(OfferLinks.Chat);

    private void DiscordButton_Click(object sender, RoutedEventArgs e) => ChatUrlAllowlist.TryOpen(PanelEndpoints.SocialDiscord);

    private void AddUserMessage(string text)
    {
        ChatMessages.Children.Add(BuildUserRow(text));
        ScrollToEnd();
    }

    private void AddBotMessage(string text, ChatCta? cta = null, ChatCta? ticketCta = null)
    {
        ChatMessages.Children.Add(BuildBotRow(text, cta, ticketCta));
        ScrollToEnd();
    }

    private void ShowTypingIndicator()
    {
        RemoveTypingIndicator();
        _typingRow = BuildTypingRow();
        ChatMessages.Children.Add(_typingRow);
        _typingPhase = 0;
        AnimateTypingDots();
        _typingTimer.Start();
        ScrollToEnd();
    }

    private void RemoveTypingIndicator()
    {
        _typingTimer.Stop();
        if (_typingRow is not null)
        {
            ChatMessages.Children.Remove(_typingRow);
            _typingRow = null;
        }
        _typingDots.Clear();
    }

    private void AnimateTypingDots()
    {
        if (_typingDots.Count == 0)
            return;

        for (var i = 0; i < _typingDots.Count; i++)
        {
            var active = i == _typingPhase;
            _typingDots[i].Opacity = active ? 1.0 : 0.28;
            _typingDots[i].Height = active ? 8 : 6;
            _typingDots[i].Width = active ? 8 : 6;
        }

        _typingPhase = (_typingPhase + 1) % _typingDots.Count;
    }

    private UIElement BuildTypingRow()
    {
        _typingDots.Clear();
        var dotsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        for (var i = 0; i < 3; i++)
        {
            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Margin = new Thickness(i == 0 ? 0 : 5, 0, 0, 0),
                Fill = (Brush)FindResource("TextGrayBrush"),
                Opacity = 0.28,
            };
            _typingDots.Add(dot);
            dotsPanel.Children.Add(dot);
        }

        var bubble = new Border
        {
            Style = (Style)FindResource("ChatBubbleBot"),
            Padding = new Thickness(16, 12, 16, 12),
            VerticalAlignment = VerticalAlignment.Top,
            Child = dotsPanel,
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 12),
        };
        row.Children.Add(BuildAvatarColumn(BotName, BotAvatar.Source, false));
        row.Children.Add(bubble);
        return row;
    }

    private UIElement BuildBotRow(string text, ChatCta? cta = null, ChatCta? ticketCta = null)
    {
        var column = new StackPanel { MaxWidth = 420 };
        column.Children.Add(BuildMessageText(text));

        if (cta is not null)
            column.Children.Add(BuildLinkButton(cta));
        if (ticketCta is not null)
            column.Children.Add(BuildLinkButton(ticketCta));

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        row.Children.Add(BuildAvatarColumn(BotName, BotAvatar.Source, false));
        row.Children.Add(new Border
        {
            Style = (Style)FindResource("ChatBubbleBot"),
            VerticalAlignment = VerticalAlignment.Top,
            Child = column,
        });
        return row;
    }

    private TextBlock BuildMessageText(string text)
    {
        var block = new TextBlock
        {
            Foreground = (Brush)FindResource("TextWhiteBrush"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        };

        var matches = UrlRegex.Matches(text);
        if (matches.Count == 0)
        {
            block.Text = text;
            return block;
        }

        var lastIndex = 0;
        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
                block.Inlines.Add(new Run(text[lastIndex..match.Index]));

            var url = match.Value.TrimEnd('.', ',', ';', ')', ']', '"', '\'');
            if (ChatUrlAllowlist.IsAllowed(url))
                block.Inlines.Add(CreateHyperlink(url, url));
            else
                block.Inlines.Add(new Run(url));
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
            block.Inlines.Add(new Run(text[lastIndex..]));

        return block;
    }

    private Hyperlink CreateHyperlink(string label, string url)
    {
        var hyperlink = new Hyperlink(new Run(label))
        {
            NavigateUri = new Uri(url, UriKind.Absolute),
            Foreground = (Brush)FindResource("AccentBrush"),
            TextDecorations = TextDecorations.Underline,
        };
        hyperlink.RequestNavigate += (_, e) =>
        {
            if (ChatUrlAllowlist.TryOpen(e.Uri.AbsoluteUri))
                e.Handled = true;
        };
        return hyperlink;
    }

    private UIElement BuildLinkButton(ChatCta cta)
    {
        var link = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            FontSize = 12,
        };
        var hyperlink = CreateHyperlink(cta.Label, cta.Url);
        hyperlink.FontWeight = FontWeights.SemiBold;
        link.Inlines.Add(hyperlink);
        return link;
    }

    private UIElement BuildUserRow(string text)
    {
        var userName = UserProfileStore.Instance.ResolvedDisplayName;
        var bubble = new Border
        {
            Style = (Style)FindResource("ChatBubbleUser"),
            MaxWidth = 280,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = text,
                Foreground = (Brush)FindResource("TextWhiteBrush"),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
            },
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 12),
        };
        row.Children.Add(bubble);
        row.Children.Add(BuildAvatarColumn(userName, UserAvatar.Source, true));
        return row;
    }

    private UIElement BuildAvatarColumn(string name, ImageSource? source, bool alignRight)
    {
        var column = new StackPanel
        {
            Width = 44,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = alignRight ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = alignRight ? new Thickness(12, 0, 0, 0) : new Thickness(0, 0, 12, 0),
        };
        column.Children.Add(BuildNameLabel(name));
        column.Children.Add(CreateAvatar(source));
        return column;
    }

    private UIElement BuildNameLabel(string name) =>
        new TextBlock
        {
            Text = name,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextGrayBrush"),
            Margin = new Thickness(0, 0, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

    private static UIElement CreateAvatar(ImageSource? source)
    {
        var avatar = new Ellipse
        {
            Width = 36,
            Height = 36,
            HorizontalAlignment = HorizontalAlignment.Center,
            Stroke = new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = 1,
        };

        if (source is not null)
        {
            avatar.Fill = new ImageBrush(source)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
            };
        }
        else
        {
            avatar.Fill = (Brush)Application.Current.FindResource("GlassSurfaceBrush");
        }

        return avatar;
    }

    private void ScrollToEnd()
    {
        ChatScroll.ScrollToEnd();
    }
}
