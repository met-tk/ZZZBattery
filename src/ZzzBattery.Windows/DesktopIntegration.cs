using System;
using System.Collections.Generic;
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
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size; public Rect Monitor, Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }
    private delegate bool MonitorCallback(IntPtr monitor, IntPtr hdc, ref Rect bounds, IntPtr data);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode)] private static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice info, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("shcore.dll")] private static extern int GetScaleFactorForMonitor(IntPtr monitor, out int scale);
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
        if (GetWindowBounds(window) is not { } bounds || MonitorFor(window) is not { } monitor) return;
        var placed = WindowPlacementMath.Clamp(bounds, monitor.WorkArea, snap);
        MoveWindow(window, placed.X, placed.Y);
    }
    public static bool IsKeyboardMonitorMove(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        static bool Down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
        return hwnd != IntPtr.Zero && GetForegroundWindow() == hwnd &&
            (Down(0x5B) || Down(0x5C)) && Down(0x10) && (Down(0x25) || Down(0x27));
    }
    public static DesktopRect? GetWindowBounds(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        return hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var rect)
            ? new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top) : null;
    }
    public static bool MoveWindow(Window window, double x, double y)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !double.IsFinite(x) || !double.IsFinite(y) || Math.Abs(x) > 10_000_000 || Math.Abs(y) > 10_000_000) return false;
        var left = (int)Math.Round(x); var top = (int)Math.Round(y);
        if (GetWindowRect(hwnd, out var rect) && rect.Left == left && rect.Top == top) return true;
        return SetWindowPos(hwnd, IntPtr.Zero, left, top, 0, 0, 0x0015); // no size, z-order or activation change
    }
    public static IReadOnlyList<DesktopMonitor> GetMonitors(Window? window = null)
    {
        var monitors = new List<DesktopMonitor>();
        var hwnd = window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr handle, IntPtr hdc, ref Rect rect, IntPtr data) =>
        {
            if (ReadMonitor(handle, hwnd) is { } monitor) monitors.Add(monitor);
            return true;
        }, IntPtr.Zero);
        return monitors;
    }
    public static DesktopMonitor? MonitorFor(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        return hwnd == IntPtr.Zero ? null : ReadMonitor(MonitorFromWindow(hwnd, 2), hwnd);
    }
    private static DesktopMonitor? ReadMonitor(IntPtr handle, IntPtr hwnd)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(handle, ref info)) return null;
        var work = info.Work;
        if (work.Right <= work.Left || work.Bottom <= work.Top) return null;
        var id = "";
        for (uint index = 0; index < 16; index++)
        {
            var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(info.Device, index, ref device, 1)) break;
            if ((device.Flags & 1) != 0 && !string.IsNullOrEmpty(device.Id)) { id = device.Id; break; }
        }
        GetScaleFactorForMonitor(handle, out var percent);
        var scale = percent is >= 25 and <= 1600 ? percent / 100d : 1;
        // The active window's effective DPI also covers custom scaling values.
        if (hwnd != IntPtr.Zero && MonitorFromWindow(hwnd, 2) == handle && GetDpiForWindow(hwnd) is var dpi && dpi > 0)
            scale = dpi / 96d;
        return new(id, info.Device, new(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top), scale, (info.Flags & 1) != 0);
    }
    public static void Startup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
        {
            var executable = Environment.ProcessPath ?? throw new IOException("找不到程序路径。");
            key.SetValue("ZZZBattery", "\"" + executable + "\" --autostart");
        }
        else key.DeleteValue("ZZZBattery", false);
    }
    public static void RefreshExistingStartupPath()
    {
        // An upgrade may move the executable to a new release directory. Only
        // update an existing entry; never re-enable an entry removed by the user.
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (key?.GetValue("ZZZBattery") is not string || Environment.ProcessPath is not { } executable) return;
        var command = "\"" + executable + "\" --autostart";
        if (!string.Equals(key.GetValue("ZZZBattery") as string, command, StringComparison.OrdinalIgnoreCase)) key.SetValue("ZZZBattery", command);
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
