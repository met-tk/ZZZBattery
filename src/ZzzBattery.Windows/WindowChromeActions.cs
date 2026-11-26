using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZzzBattery.Windows;

internal static class WindowChromeActions
{
    public static void DragMove(Window window, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && window.ResizeMode == ResizeMode.CanResize)
        {
            ToggleMaximize(window); e.Handled = true; return;
        }
        if (e.LeftButton != MouseButtonState.Pressed) return;
        try { window.DragMove(); } catch (InvalidOperationException) { }
    }

    public static void ToggleMaximize(Window window) =>
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    public static void Update(Window window, Button maximizeButton, TextBlock glyph)
    {
        var maximized = window.WindowState == WindowState.Maximized;
        glyph.Text = maximized ? "❐" : "□";
        var label = maximized ? "还原" : "最大化";
        maximizeButton.ToolTip = label;
        AutomationProperties.SetName(maximizeButton, label);
    }
}
