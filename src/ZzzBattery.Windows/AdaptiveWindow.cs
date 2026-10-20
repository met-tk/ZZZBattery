using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

// Auxiliary windows follow their source display on first open, then the display
// the user moves them to. Temporary display constraints do not change their
// preferred size. This controller never touches the widget's saved placement.
internal sealed class AdaptiveWindow
{
    private static readonly ConditionalWeakTable<Window, AdaptiveWindow> Controllers = new();
    private readonly Window _window;
    private readonly Window? _origin;
    private readonly double _minimumWidth, _minimumHeight;
    private double _preferredWidth, _preferredHeight;
    private readonly double _contentMinimumWidth, _contentMinimumHeight;
    private readonly ScrollViewer? _viewport;
    private readonly FrameworkElement? _content;
    private HwndSource? _source;
    private bool _loaded, _closed, _queued, _applying, _moving, _resizing, _center = true;

    public static AdaptiveWindow Attach(Window window, double contentWidth = 320, double contentHeight = 300)
    {
        if (Controllers.TryGetValue(window, out var attached)) return attached;
        var controller = new AdaptiveWindow(window, contentWidth, contentHeight);
        Controllers.Add(window, controller);
        return controller;
    }

    private AdaptiveWindow(Window window, double contentWidth, double contentHeight)
    {
        _window = window;
        _origin = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w != window && w.IsActive)
            ?? (Application.Current.MainWindow != window ? Application.Current.MainWindow : null);
        _preferredWidth = window.Width; _preferredHeight = window.Height;
        _minimumWidth = window.MinWidth; _minimumHeight = window.MinHeight;
        _contentMinimumWidth = contentWidth; _contentMinimumHeight = contentHeight;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.UseLayoutRounding = true;
        if (window.Content is FrameworkElement content)
        {
            _content = content;
            window.Content = null;
            _viewport = new ScrollViewer
            {
                Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Style = (Style)window.FindResource("WidgetScrollViewer")
            };
            window.Content = _viewport;
            _viewport.SizeChanged += (_, _) => FitContent();
            _viewport.ScrollChanged += (_, e) =>
            {
                if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0) FitContent();
            };
        }
        window.SourceInitialized += (_, _) =>
        {
            _source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            _source?.AddHook(WindowMessage);
            // Seed the native position on the source screen before first paint.
            if (InitialMonitor() is { } monitor)
            {
                var center = DisplayLayoutMath.Center(monitor.WorkArea,
                    Math.Min(_preferredWidth * monitor.Scale, monitor.WorkArea.Width),
                    Math.Min(_preferredHeight * monitor.Scale, monitor.WorkArea.Height));
                DesktopIntegration.MoveWindow(window, center.X, center.Y);
            }
        };
        window.Loaded += (_, _) => { _loaded = true; QueueFit(); };
        window.LocationChanged += (_, _) => { if (!_applying && !_moving) QueueFit(); };
        window.SizeChanged += (_, _) => FitContent();
        window.StateChanged += (_, _) => QueueFit();
        window.Closed += (_, _) =>
        {
            _closed = true;
            _source?.RemoveHook(WindowMessage); _source = null;
        };
    }

    private DesktopMonitor? InitialMonitor()
    {
        var origin = _window.Owner ?? _origin;
        return (origin is not null ? DesktopIntegration.MonitorFor(origin) : null)
            ?? DesktopIntegration.GetMonitors().FirstOrDefault(m => m.Primary);
    }

    private void FitContent()
    {
        if (_closed || _viewport is null || _content is null) return;
        var width = _viewport.ViewportWidth > 0 ? _viewport.ViewportWidth : _viewport.ActualWidth;
        var height = _viewport.ViewportHeight > 0 ? _viewport.ViewportHeight : _viewport.ActualHeight;
        if (width <= 0 || height <= 0) return;
        var margin = _content.Margin;
        var desiredWidth = Math.Max(_contentMinimumWidth, width - margin.Left - margin.Right);
        var desiredHeight = Math.Max(_contentMinimumHeight, height - margin.Top - margin.Bottom);
        if (Math.Abs(_content.Width - desiredWidth) > .1 || double.IsNaN(_content.Width)) _content.Width = desiredWidth;
        if (Math.Abs(_content.Height - desiredHeight) > .1 || double.IsNaN(_content.Height)) _content.Height = desiredHeight;
    }

    private void QueueFit()
    {
        if (!_loaded || _closed || _queued || _applying || _moving) return;
        _queued = true;
        _window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _queued = false;
            if (_closed || _moving || App.Current.Exiting) return;
            var monitor = _center ? InitialMonitor() : DesktopIntegration.MonitorFor(_window);
            if (monitor is not null) Fit(monitor, _center);
            _center = false;
        }));
    }

    private void Fit(DesktopMonitor monitor, bool center)
    {
        if (_applying || _window.WindowState == WindowState.Minimized) return;
        _applying = true;
        try
        {
            var fit = DisplayLayoutMath.Fit(monitor, _preferredWidth, _preferredHeight, _minimumWidth, _minimumHeight);
            // Lower minimums before maximums. Old MinWidth must not override a
            // newly smaller monitor, and old MaxWidth must not block expansion.
            _window.MinWidth = 0; _window.MinHeight = 0;
            _window.MaxWidth = fit.MaximumWidth; _window.MaxHeight = fit.MaximumHeight;
            _window.MinWidth = fit.MinimumWidth; _window.MinHeight = fit.MinimumHeight;
            if (_window.WindowState == WindowState.Maximized)
            {
                _window.UpdateLayout(); FitContent(); return;
            }
            _window.Width = fit.Width; _window.Height = fit.Height;
            _window.UpdateLayout(); FitContent();
            if (DesktopIntegration.GetWindowBounds(_window) is not { } bounds) return;
            var placed = center ? DisplayLayoutMath.Center(monitor.WorkArea, bounds.Width, bounds.Height)
                : WindowPlacementMath.Clamp(bounds, monitor.WorkArea);
            DesktopIntegration.MoveWindow(_window, placed.X, placed.Y);
        }
        finally { _applying = false; }
    }

    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (message)
        {
            case 0x0231: _moving = true; _resizing = false; break;
            case 0x0214: _resizing = true; break; // WM_SIZING is an explicit user resize.
            case 0x0232:
                _moving = false;
                if (_resizing) { _preferredWidth = _window.Width; _preferredHeight = _window.Height; }
                QueueFit(); break;
            case 0x02E0: // WPF processes the DPI suggestion before the queued fit.
            case 0x007E:
            case 0x001A: QueueFit(); break;
        }
        return IntPtr.Zero;
    }

    // Isolated tests exercise the production fit path without changing displays.
    internal void FitForCheck(DesktopMonitor monitor) => Fit(monitor, center: true);
}
