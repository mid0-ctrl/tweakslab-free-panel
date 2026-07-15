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

    private const string WelcomeMessage =
        "Salut ! Je suis Bryan, ton assistant PC. Pose-moi une question sur l'optimisation, les performances ou le dépannage.";

    private readonly List<ChatMessage> _history = [];
    private readonly DispatcherTimer _typingTimer = new() { Interval = TimeSpan.FromMilliseconds(380) };
    private readonly List<Ellipse> _typingDots = [];
    private int _typingPhase;
    private bool _sending;
    private UIElement? _typingRow;

    public AssistanceView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshUserIdentity();
        BotAvatar.Source = LoadBotAvatar();
        RefreshUserIdentity();
        _typingTimer.Tick += (_, _) => AnimateTypingDots();
        AddBotMessage(WelcomeMessage);
        ChatInput.KeyDown += ChatInput_KeyDown;
    }

    private static ImageSource LoadBotAvatar() =>
        new BitmapImage(new Uri(AssetHelper.AssistantBryan, UriKind.Absolute));

    private void RefreshUserIdentity()
    {
        var profile = UserProfileStore.Instance;
        UserAvatar.Source = profile.ResolveAvatarSource();
    }

    private void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            Send_Click(sender, e);
            e.Handled = true;
        }
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        if (_sending)
            return;

        var text = ChatInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        RefreshUserIdentity();
        ChatInput.Text = string.Empty;
        AddUserMessage(text);
        _history.Add(new ChatMessage("user", text));

        _sending = true;
        SendButton.IsEnabled = false;
        ChatInput.IsEnabled = false;
        ShowTypingIndicator();

        try
        {
            var result = await ChatService.SendAsync(text, _history.Take(_history.Count - 1).ToList());
            RemoveTypingIndicator();

            if (result is null)
            {
                AddBotMessage("Je n'ai pas pu joindre l'assistant pour le moment. Réessaie dans un instant.");
                return;
            }

            _history.Add(new ChatMessage("assistant", result.Reply));
            AddBotMessage(result.Reply, result.Cta, result.TicketCta);
        }
        catch
        {
            RemoveTypingIndicator();
            AddBotMessage("Je n'ai pas pu joindre l'assistant pour le moment. Réessaie dans un instant.");
        }
        finally
        {
            _sending = false;
            SendButton.IsEnabled = true;
            ChatInput.IsEnabled = true;
            StatusText.Text = string.Empty;
            ChatInput.Focus();
        }
    }

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
