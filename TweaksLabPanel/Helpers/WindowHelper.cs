using System.Windows;
using System.Windows.Input;

namespace TweaksLabPanel.Helpers;

public static class WindowHelper
{
    public static void EnableDrag(Window window, UIElement dragHandle)
    {
        dragHandle.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 1)
                window.DragMove();
        };
    }
}
