using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

// window.open must keep its own WindowProxy and opener, especially for cross-domain login.
internal sealed class OfficialPopupHost(Window owner, Action<string> report)
{
    private readonly HashSet<Window> _windows = new();
    public static bool Allowed(string address) => address == "about:blank" ||
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme == "https" && Cookies.IsOfficialHost(uri.Host);
    public void Attach(CoreWebView2 core) => core.NewWindowRequested += async (_, e) => await OpenAsync(core, e);
    public void CloseAll() { foreach (var window in _windows.ToArray()) window.Close(); }
    private async Task OpenAsync(CoreWebView2 opener, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (!Allowed(e.Uri) || _windows.Count >= 4) { report("已阻止非官方页面或过多弹窗。"); return; }
        var deferral = e.GetDeferral();
        Window? popup = null;
        try
        {
            var browser = new WebView2();
            var address = new TextBlock { Margin = new Thickness(12), Text = "正在加载官方登录页面…", TextWrapping = TextWrapping.Wrap };
            var panel = new DockPanel(); DockPanel.SetDock(address, Dock.Top);
            panel.Children.Add(address); panel.Children.Add(browser);
            popup = new Window
            {
                Title = "米游社官方登录", Owner = owner,
                Width = 1000, Height = 760,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel
            };
            AdaptiveWindow.Attach(popup, 320, 300);
            var closed = false;
            var active = popup;
            _windows.Add(active);
            active.Closed += (_, _) => { closed = true; _windows.Remove(active); browser.Dispose(); };
            active.Show();
            var environment = opener.Environment;
            var options = environment.CreateCoreWebView2ControllerOptions();
            options.ProfileName = opener.Profile.ProfileName;
            options.IsInPrivateModeEnabled = opener.Profile.IsInPrivateModeEnabled;
            await browser.EnsureCoreWebView2Async(environment, options);
            if (closed) return;
            var child = browser.CoreWebView2;
            child.Settings.IsPasswordAutosaveEnabled = false;
            child.Settings.IsGeneralAutofillEnabled = false;
            child.NavigationStarting += (_, navigation) =>
            {
                if (!Allowed(navigation.Uri)) { navigation.Cancel = true; report("已阻止非米游社官方页面跳转。"); return; }
                address.Text = navigation.Uri == "about:blank" ? "官方登录窗口" : new Uri(navigation.Uri).GetLeftPart(UriPartial.Path);
            };
            child.DownloadStarting += (_, download) => download.Cancel = true;
            child.WindowCloseRequested += (_, _) => active.Close();
            Attach(child);
            // Do not Navigate here: WebView2 completes the original popup navigation and preserves window.opener.
            e.NewWindow = child;
        }
        catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException)
        { popup?.Close(); report("无法打开官方登录弹窗，请重载页面后重试。"); }
        finally { deferral.Complete(); }
    }
}
