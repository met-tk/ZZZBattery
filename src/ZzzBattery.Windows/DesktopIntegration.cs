using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

internal static class DesktopIntegration
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    public static void ClickThrough(Window window, bool enabled)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var style = GetWindowLongPtr(hwnd, -20).ToInt64();
        style = enabled ? style | 0x20L | 0x80000L : style & ~0x20L;
        SetWindowLongPtr(hwnd, -20, new IntPtr(style));
    }
    public static void KeepVisible(Window window, bool snap = false)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect)) return;
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref monitor)) return;
        var width = rect.Right - rect.Left; var height = rect.Bottom - rect.Top;
        var x = Math.Clamp(rect.Left, monitor.Work.Left, Math.Max(monitor.Work.Left, monitor.Work.Right - width));
        var y = Math.Clamp(rect.Top, monitor.Work.Top, Math.Max(monitor.Work.Top, monitor.Work.Bottom - height));
        if (snap)
        {
            if (Math.Abs(x - monitor.Work.Left) <= 16) x = monitor.Work.Left;
            if (Math.Abs(x + width - monitor.Work.Right) <= 16) x = monitor.Work.Right - width;
            if (Math.Abs(y - monitor.Work.Top) <= 16) y = monitor.Work.Top;
            if (Math.Abs(y + height - monitor.Work.Bottom) <= 16) y = monitor.Work.Bottom - height;
        }
        if (x != rect.Left || y != rect.Top) SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, 0x0015);
    }
    public static void Startup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
        {
            var executable = Environment.ProcessPath ?? throw new IOException("找不到程序路径。");
            key.SetValue("ZZZBattery", "\"" + executable + "\"");
        }
        else key.DeleteValue("ZZZBattery", false);
    }
}

internal static class Palette
{
    public static void Apply(WidgetSettings settings)
    {
        var light = settings.Theme == "Light";
        if (settings.Theme == "System")
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            light = (key?.GetValue("AppsUseLightTheme") as int? ?? 0) != 0;
        }
        void Set(string name, string color)
        {
            var value = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color);
            // Ordinary settings changes should not invalidate every themed element in every window.
            if (Application.Current.Resources[name] is System.Windows.Media.SolidColorBrush current && current.Color == value) return;
            Application.Current.Resources[name] = new System.Windows.Media.SolidColorBrush(value);
        }
        Set("Surface", light ? "#F4F5EE" : "#191B1C");
        Set("Panel", light ? "#E8EADF" : "#242729");
        Set("Ink", light ? "#171B14" : "#F4F5EB");
        Set("Muted", light ? "#5F6758" : "#A3AAA0");
        Set("Stroke", light ? "#CDD2C4" : "#383D37");
        Set("Accent", settings.Accent == "Yellow" ? "#F3D95A" : "#D6EF69");
        Set("AccentText", light ? (settings.Accent == "Yellow" ? "#756015" : "#526518") : (settings.Accent == "Yellow" ? "#F3D95A" : "#D6EF69"));
        Set("Warning", light ? "#8A4A00" : "#FFD17C");
    }
}
