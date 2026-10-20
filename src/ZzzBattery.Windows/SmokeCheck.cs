using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

// An explicit development command; never runs during normal startup or accesses a real account.
internal static class SmokeCheck
{
    public static async Task RunAsync(App app, MainWindow window)
    {
        try
        {
            await Task.Delay(250);
            if (app.Store.Read<WidgetSettings>("settings.json")?.DeviceId != app.Settings.DeviceId)
                throw new Exception("Device identity was not persisted before first network use");
            var sample = Cookies.Create("account_id=123456; cookie_token=smoke-test-only");
            sample.Role = new("100000001", "prod_gf_cn", "测试角色", "测试区服");
            var isolated = new LocalStore(Path.Combine(app.Store.Root, "crypto-check"));
            isolated.SaveCredential(sample);
            if (isolated.LoadCredential()?.Cookie != sample.Cookie) throw new Exception("DPAPI round trip failed");
            if (File.ReadAllText(Path.Combine(isolated.Root, "credentials.dpapi")).Contains("smoke-test-only"))
                throw new Exception("Plaintext credential leak");
            isolated.Write("sync-state.json", new SyncCheckpoint(sample.AccountKey, sample.Role.Key, DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(15), TimeSpan.Zero, FailureKind.Verification, 10035));
            isolated.ClearAccount();
            if (File.Exists(Path.Combine(isolated.Root, "sync-state.json")) || isolated.LoadCredential() is not null)
                throw new Exception("Sign out did not clear credentials and sync state");
            await WallpaperSmokeCheck.RunAsync(app, window);
            app.Demo = false; app.Settings.CharacterStyle = false; app.Settings.Expanded = false;
            window.ApplySettings();
            Capture(window, Path.Combine(app.Store.Root, "first-run.png"));
            app.Demo = true; window.Render();
            await Task.Delay(100);
            Capture(window, Path.Combine(app.Store.Root, "standard.png"));
            app.Settings.Expanded = true; window.ApplySettings();
            await Task.Delay(100);
            Capture(window, Path.Combine(app.Store.Root, "expanded.png"));
            app.Settings.Mini = true; window.ApplySettings();
            await Task.Delay(100);
            Capture(window, Path.Combine(app.Store.Root, "mini.png"));
            app.Settings.Mini = false; app.Settings.Theme = "Light";
            Palette.Apply(app.Settings); window.ApplySettings();
            app.SaveSettings();
            var saved = app.Store.Read<WidgetSettings>("settings.json");
            if (saved is not { Theme: "Light", Mini: false, Expanded: true }) throw new Exception("Settings persistence failed");
            await Task.Delay(100);
            Capture(window, Path.Combine(app.Store.Root, "light.png"));
            var settings = new SettingsWindow(); settings.Show(); settings.UpdateLayout();
            Capture(settings, Path.Combine(app.Store.Root, "settings.png")); settings.Close();
            // This test opens only a fresh, isolated official web page. It never authenticates.
            var account = new AccountWindow(); account.Show();
            var deadline = DateTimeOffset.UtcNow.AddSeconds(25);
            while (!account.OfficialPageLoaded && DateTimeOffset.UtcNow < deadline) await Task.Delay(200);
            var webViewReady = account.BrowserReady;
            var officialPageLoaded = account.OfficialPageLoaded;
            if (officialPageLoaded)
            {
                await Task.Delay(1500);
                await account.CaptureOfficialPageAsync(Path.Combine(app.Store.Root, "official-web-page.png"));
            }
            Capture(account, Path.Combine(app.Store.Root, "login-shell.png"));
            account.Close();
            if (!webViewReady) throw new Exception("WebView2 could not initialize from the published executable");
            await VerifyPopupReturn(app);
            // Same production verification window, anonymous and isolated. No
            // credentials are injected and no user challenge is completed by tests.
            var verification = new VerificationWindow(new Credential(), null);
            verification.Show();
            var pageDeadline = DateTimeOffset.UtcNow.AddSeconds(25);
            while (!verification.PageReady && !verification.PageFailed && DateTimeOffset.UtcNow < pageDeadline) await Task.Delay(200);
            if (!verification.PageReady)
            {
                verification.Close();
                throw new Exception("Verification page did not render readable official content.");
            }
            await verification.CapturePageAsync(Path.Combine(app.Store.Root, "verification-official-page.png"));
            if (!await verification.OfficialContentFitsForCheck()) throw new Exception("Official login content is clipped by the browser viewport.");
            verification.NavigateBlankForCheck();
            var blankDeadline = DateTimeOffset.UtcNow.AddSeconds(25);
            while (!verification.PageFailed && DateTimeOffset.UtcNow < blankDeadline) await Task.Delay(200);
            if (!verification.PageFailed) throw new Exception("Blank page did not enter the recoverable timeout state.");
            Capture(verification, Path.Combine(app.Store.Root, "verification-timeout.png"));
            verification.Close();
            File.WriteAllText(Path.Combine(app.Store.Root, "smoke-result.json"),
                JsonSerializer.Serialize(new { passed = true, encryption = "Windows DPAPI CurrentUser",
                    webViewReady, officialPageLoaded, settingsPersisted = true, deviceIdentityPersisted = true,
                    signOutClearsSyncState = true, popupOpenerRoundTrip = true,
                    verificationPageRendered = true, verificationContentFits = true, blankPageTimeout = true,
                    rendering = new[] { "first-run", "standard", "expanded", "mini", "light", "settings", "login-shell" } }));
            app.Quit();
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(app.Store.Root, "smoke-result.json"), JsonSerializer.Serialize(new { passed = false, error = error.ToString() }));
            app.Quit();
        }
    }
    private static async Task VerifyPopupReturn(App app)
    {
        var browser = new Microsoft.Web.WebView2.Wpf.WebView2
        {
            CreationProperties = new Microsoft.Web.WebView2.Wpf.CoreWebView2CreationProperties
            { UserDataFolder = Path.Combine(app.Store.Root, "popup-check"), IsInPrivateModeEnabled = true }
        };
        var owner = new System.Windows.Window { Width = 500, Height = 320, Content = browser, ShowInTaskbar = false, Title = "Popup self-test" };
        var popups = new OfficialPopupHost(owner, _ => { });
        owner.Show();
        try
        {
            await browser.EnsureCoreWebView2Async();
            popups.Attach(browser.CoreWebView2);
            var loaded = new TaskCompletionSource();
            browser.CoreWebView2.NavigationCompleted += (_, _) => loaded.TrySetResult();
            browser.CoreWebView2.NavigateToString("<html><body>Local popup return test</body></html>");
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await browser.CoreWebView2.ExecuteScriptAsync("""
                window.__popupPassed=false;
                window.addEventListener('message',e=>{if(e.data==='popup-roundtrip')window.__popupPassed=true;});
                const child=window.open('about:blank','self-test');
                setTimeout(()=>{child.document.write('<script>window.opener.postMessage("popup-roundtrip","*");window.close();<\/script>');child.document.close();},200);
                """);
            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (await browser.CoreWebView2.ExecuteScriptAsync("window.__popupPassed") == "true") return;
                await Task.Delay(100);
            }
            throw new Exception("Login popup lost its window.opener callback");
        }
        finally { popups.CloseAll(); browser.Dispose(); owner.Close(); }
    }
    private static void Capture(System.Windows.Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
