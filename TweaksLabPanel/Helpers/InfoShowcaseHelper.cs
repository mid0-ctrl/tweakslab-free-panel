using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TweaksLabPanel.Helpers;

public static class InfoShowcaseHelper
{
    private static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(90);

    public static void WireHover(Border badge, FrameworkElement panel, TextBlock messageText, Func<string> getMessage)
    {
        messageText.Text = getMessage();

        var hideTimer = new DispatcherTimer { Interval = HideDelay };
        hideTimer.Tick += (_, _) =>
        {
            hideTimer.Stop();
            if (!badge.IsMouseOver && !panel.IsMouseOver)
                SetOpen(panel, badge, false);
        };

        void Show()
        {
            hideTimer.Stop();
            SetOpen(panel, badge, true);
        }

        void ScheduleHide()
        {
            hideTimer.Stop();
            hideTimer.Start();
        }

        badge.MouseEnter += (_, _) => Show();
        badge.MouseLeave += (_, _) => ScheduleHide();
        panel.MouseEnter += (_, _) => Show();
        panel.MouseLeave += (_, _) => ScheduleHide();
    }

    public static void RefreshText(TextBlock messageText, Func<string> getMessage) =>
        messageText.Text = getMessage();

    public static void Collapse(FrameworkElement panel, Border badge) =>
        SetOpen(panel, badge, false);

    private static void SetOpen(FrameworkElement panel, Border badge, bool open)
    {
        panel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        badge.Background = open
            ? (Brush)badge.FindResource("AccentSoftBrush")
            : (Brush)badge.FindResource("InfoBadgeBgBrush");
        badge.BorderBrush = open
            ? (Brush)badge.FindResource("AccentSoftBorderBrush")
            : (Brush)badge.FindResource("InfoBadgeBorderBrush");
    }
}
