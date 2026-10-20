using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

// Keep the user's intended position separate from temporary visibility fixes.
// All native rectangles use screen pixels; only monitor-relative offsets are DIP.
internal sealed class WindowPlacementController
{
    private readonly MainWindow _window;
    private readonly WidgetSettings _settings;
    private readonly DispatcherTimer _startupTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _keyboardTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private HwndSource? _source;
    private bool _loaded, _closed, _queued, _applying, _dragging, _userMoved, _keyboardMove;
    private int _startupPasses;
    private WindowPlacement? _preferred;

    public WindowPlacementController(MainWindow window, WidgetSettings settings)
    {
        _window = window; _settings = settings;
        _preferred = WindowPlacementMath.IsValid(settings.Placement) ? settings.Placement : null;
        window.SourceInitialized += (_, _) =>
        {
            _source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            _source?.AddHook(WindowMessage);
        };
        window.Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            // Migrate legacy WPF coordinates once, before our own visibility
            // correction. Older files cannot identify a missing original monitor.
            if (_preferred is null && settings.Left is { } x && settings.Top is { } y &&
                double.IsFinite(x) && double.IsFinite(y) &&
                DesktopIntegration.GetWindowBounds(window) is { Width: > 0, Height: > 0 } bounds &&
                DesktopIntegration.MonitorFor(window) is { } monitor)
            {
                var migrated = WindowPlacementMath.Capture(bounds, monitor) with
                {
                    AnchorRight = false, AnchorBottom = false,
                    OffsetX = (bounds.X - monitor.WorkArea.X) / monitor.Scale,
                    OffsetY = (bounds.Y - monitor.WorkArea.Y) / monitor.Scale
                };
                if (WindowPlacementMath.IsValid(migrated))
                {
                    _preferred = settings.Placement = migrated;
                    App.Current.SavePosition();
                }
            }
            QueueRestore();
            // Display enumeration/work areas can settle after Explorer starts.
            // Event hooks remain active after this bounded startup catch-up.
            _startupTimer.Start();
        };
        window.LocationChanged += (_, _) =>
        {
            if (_dragging) _userMoved = true;
            else if (!_applying) QueueRestore();
        };
        window.SizeChanged += (_, _) => { if (!_applying) QueueRestore(); };
        window.IsVisibleChanged += (_, _) => { if (window.IsVisible) QueueRestore(); };
        window.Closed += (_, _) =>
        {
            _closed = true; _startupTimer.Stop(); _keyboardTimer.Stop();
            _source?.RemoveHook(WindowMessage); _source = null;
        };
        _startupTimer.Tick += (_, _) =>
        {
            QueueRestore();
            if (++_startupPasses >= 15) _startupTimer.Stop();
        };
        _keyboardTimer.Tick += (_, _) =>
        {
            _keyboardTimer.Stop();
            if (_keyboardMove) { _keyboardMove = false; EndUserMove(); }
        };
    }

    public void BeginUserMove()
    {
        if (_closed || _dragging) return;
        _dragging = true; _userMoved = false;
    }

    public void EndUserMove()
    {
        if (!_dragging || _closed) return;
        _dragging = false;
        if (!_userMoved) { QueueRestore(); return; }
        _applying = true;
        try
        {
            _window.UpdateLayout();
            if (DesktopIntegration.MonitorFor(_window) is { } monitor)
                FitDisplay(monitor);
            DesktopIntegration.KeepVisible(_window, snap: true);
            SaveUserPosition(); // synchronous: quitting immediately cannot lose it
        }
        finally { _applying = false; _userMoved = false; }
        QueueRestore();
    }

    public void FlushUserPosition()
    {
        // Never flush an automatic clamp/fallback over the preferred position.
        if (_dragging && _userMoved && !_closed) SaveUserPosition();
    }

    private void SaveUserPosition()
    {
        if (DesktopIntegration.GetWindowBounds(_window) is not { Width: > 0, Height: > 0 } bounds ||
            DesktopIntegration.MonitorFor(_window) is not { } monitor) return;
        var preferred = WindowPlacementMath.Capture(bounds, monitor);
        if (!WindowPlacementMath.IsValid(preferred)) return;
        _preferred = _settings.Placement = preferred;
        // Retain these for older versions, never use them once Placement exists.
        _settings.Left = _window.Left; _settings.Top = _window.Top;
        App.Current.SavePosition();
    }

    public void QueueRestore()
    {
        if (!_loaded || _closed || _queued || _dragging || App.Current.Exiting) return;
        _queued = true;
        _window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _queued = false;
            if (!_closed && !_dragging && !App.Current.Exiting) Restore();
        }));
    }

    private void Restore()
    {
        if (_applying || _window.WindowState != WindowState.Normal ||
            DesktopIntegration.GetWindowBounds(_window) is not { Width: > 0, Height: > 0 } bounds) return;
        _applying = true;
        try
        {
            var monitors = DesktopIntegration.GetMonitors(_window);
            var target = WindowPlacementMath.Restore(_preferred, monitors, bounds.Width, bounds.Height);
            if (target is null) return;
            FitDisplay(target.Monitor);
            // Move to the right display first. WPF owns WM_DPICHANGED and lays
            // out at the destination DPI; read the final native bounds afterward.
            DesktopIntegration.MoveWindow(_window, target.Bounds.X, target.Bounds.Y);
            var current = DesktopIntegration.MonitorFor(_window) ?? target.Monitor;
            FitDisplay(current);
            if (DesktopIntegration.GetWindowBounds(_window) is not { } finalBounds) return;
            target = WindowPlacementMath.Restore(_preferred, DesktopIntegration.GetMonitors(_window), finalBounds.Width, finalBounds.Height);
            if (target is not null) DesktopIntegration.MoveWindow(_window, target.Bounds.X, target.Bounds.Y);
        }
        finally { _applying = false; }
    }

    private void FitDisplay(DesktopMonitor monitor)
    {
        var fit = DisplayLayoutMath.Fit(monitor, _window.Width, Math.Max(1, _window.ActualHeight), 1, 1);
        if (Math.Abs(_window.MaxWidth - fit.MaximumWidth) > .1 || double.IsInfinity(_window.MaxWidth)) _window.MaxWidth = fit.MaximumWidth;
        if (Math.Abs(_window.MaxHeight - fit.MaximumHeight) > .1 || double.IsInfinity(_window.MaxHeight)) _window.MaxHeight = fit.MaximumHeight;
        _window.UpdateLayout();
    }

    internal void FitDisplayForCheck(DesktopMonitor monitor) => FitDisplay(monitor);

    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (message)
        {
            case 0x0046: // WM_WINDOWPOSCHANGING: latch only explicit foreground keyboard intent.
            case 0x0047:
                if (!_applying && !_dragging && DesktopIntegration.IsKeyboardMonitorMove(_window))
                { _keyboardMove = true; BeginUserMove(); }
                if (_keyboardMove) { _keyboardTimer.Stop(); _keyboardTimer.Start(); }
                break;
            case 0x0231: _keyboardMove = false; _keyboardTimer.Stop(); BeginUserMove(); break; // WM_ENTERSIZEMOVE
            case 0x0232: EndUserMove(); break;   // WM_EXITSIZEMOVE
            case 0x02E0: // WM_DPICHANGED; let WPF process its suggested rectangle
            case 0x007E: // WM_DISPLAYCHANGE
            case 0x001A: // WM_SETTINGCHANGE (including taskbar/work area)
                QueueRestore(); break;
        }
        return IntPtr.Zero;
    }
}
