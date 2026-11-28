using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

// Entirely intercepted fixtures, isolated by the existing --data-dir smoke guard.
internal static class BackgroundSmokeCheck
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    public static async Task RunAsync(App app)
    {
        var processes = new HashSet<uint> { (uint)Environment.ProcessId };
        var stoleFocus = false;
        var focusCheck = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        focusCheck.Tick += (_, _) =>
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var process);
            if (processes.Contains(process)) stoleFocus = true;
        };
        focusCheck.Start();
        try
        {
            var coldBoot = File.Exists(Path.Combine(app.Store.Root, "cold-boot-fixture"));
            var credential = Cookies.Create("account_id=123; cookie_token=" + (coldBoot ? "explicit-login" : "background-test-only"));
            credential.Role = new("100", "prod_gf_cn", "测试", "测试");
            var request = MiyousheClient.NoteEndpoint + "?role_id=100&server=prod_gf_cn";
            var valid = """{"retcode":0,"data":{"energy":{"progress":{"current":119,"max":240},"restore":21780}}}""";
            var requests = 0; var pages = 0; var popups = 0; var controllers = 0;
            CoreWebView2? observed = null;
            var restoredSessionSent = false;
            var coldBootSessionSent = false;
            void Configure(CoreWebView2 core)
            {
                observed = core;
                processes.Add(core.BrowserProcessId);
                controllers++;
                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All,
                    CoreWebView2WebResourceRequestSourceKinds.All);
                core.NewWindowRequested += (_, e) =>
                {
                    if (!e.Handled) throw new Exception("Popup was not blocked");
                    popups++;
                };
                core.WebResourceRequested += (_, e) =>
                {
                    var note = e.Request.Uri == request;
                    if (!note && e.ResourceContext != CoreWebView2WebResourceContext.Document)
                    {
                        e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(), 204, "No Content", "");
                        return;
                    }
                    if (note)
                    {
                        requests++;
                        if (controllers == 1 && requests == 1)
                            coldBootSessionSent = e.Request.Headers.Contains("Cookie") &&
                                e.Request.Headers.GetHeader("Cookie").Contains("recovery_session=session-test-only");
                        if (controllers == 2)
                            restoredSessionSent = e.Request.Headers.Contains("Cookie") &&
                                e.Request.Headers.GetHeader("Cookie").Contains("recovery_session=session-test-only");
                    }
                    else pages++;
                    var body = note ? valid : "<!doctype html><link rel=\"icon\" href=\"data:,\"><body>后台读取测试<script>" +
                        "alert('must stay silent');window.open('https://example.com/blocked');" +
                        "location.href=" + JsonSerializer.Serialize(request) + ";</script></body>";
                    e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(body)),
                        200, "OK", "Content-Type: " + (note ? "application/json" : "text/html; charset=utf-8") +
                        "\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-store" + (note ?
                        "\r\nSet-Cookie: cookie_token=rotated-test-only; Domain=.mihoyo.com; Path=/; Secure; HttpOnly" +
                        "\r\nSet-Cookie: recovery_session=session-test-only; Path=/; Secure; HttpOnly; SameSite=Lax" : ""));
                };
            }
            using (var browser = new OfficialNoteBrowser(app.Store))
            {
                var first = await browser.ReadForCheckAsync(credential, Configure);
                if (coldBoot && !coldBootSessionSent)
                    throw new Exception("Encrypted session was not restored in the new process/profile");
                if (first.Current != 119 || first.Source != "官方网页便笺" || !browser.InvisibleForCheck)
                    throw new Exception("Invisible response capture failed");
                var second = await browser.ReadForCheckAsync(credential, _ => { });
                if (second.Current != 119 || requests != 2 || pages != 2 || controllers != 1 || popups != 2)
                    throw new Exception($"Unexpected requests/lifetime: notes={requests}, pages={pages}, controllers={controllers}, popups={popups}");
                if (!credential.Cookie.Contains("cookie_token=rotated-test-only") ||
                    app.Store.LoadCredential()?.Cookie != credential.Cookie)
                    throw new Exception("Rotated browser credential was not persisted");
                if (File.ReadAllText(Path.Combine(app.Store.Root, "browser-session.dpapi")).Contains("session-test-only"))
                    throw new Exception("Browser session was stored in plaintext");
                // Removing all live cookies simulates the loss of session cookies on a cold boot.
                browser.ClearSessionCookies();
                if ((await observed!.CookieManager.GetCookiesAsync(null)).Count != 0)
                    throw new Exception("Fixture cookies were not cleared");
            }
            credential = app.Store.LoadCredential()!;
            using (var restarted = new OfficialNoteBrowser(app.Store))
            {
                // This must initialize the saved profile even before the first background page read.
                await restarted.TryRefreshCredentialAsync(credential, CancellationToken.None);
                if (!restarted.InvisibleForCheck) throw new Exception("Credential recovery did not create an invisible browser");
                await restarted.ReadForCheckAsync(credential, Configure);
                if (!restoredSessionSent) throw new Exception("Restored session cookie was not sent on the first request after restart");
                var cookies = await observed!.CookieManager.GetCookiesAsync(null);
                var sessionCookie = cookies.SingleOrDefault(c => c.Name == "recovery_session");
                if (sessionCookie is not { IsSession: true, IsHttpOnly: true, IsSecure: true } ||
                    sessionCookie.Value != "session-test-only")
                    throw new Exception("Session cookie scope/flags were not restored");
                // A newly rotated browser cookie must not be overwritten by an older credential.
                var newer = observed.CookieManager.CreateCookie("cookie_token", "newer-browser-token", ".mihoyo.com", "/");
                newer.IsSecure = true; observed.CookieManager.AddOrUpdateCookie(newer);
                await OfficialBrowserSession.PrepareAsync(observed, credential, app.Store, CancellationToken.None);
                if (!(await observed.CookieManager.GetCookiesAsync(MiyousheClient.NoteEndpoint))
                    .Any(c => c.Name == "cookie_token" && c.Value == "newer-browser-token"))
                    throw new Exception("Opening a window overwrote a newer cookie");
                var pageToken = observed.CookieManager.CreateCookie("ltoken", "page-only-token", "act.mihoyo.com", "/");
                pageToken.IsSecure = true; observed.CookieManager.AddOrUpdateCookie(pageToken);
                credential.Cookie = Cookies.Create("account_id=123; cookie_token=http-rotation").Cookie;
                await OfficialBrowserSession.PrepareAsync(observed, credential, app.Store, CancellationToken.None);
                if (!(await observed.CookieManager.GetCookiesAsync("https://act.mihoyo.com/"))
                    .Any(c => c.Name == "ltoken" && c.Value == "page-only-token"))
                    throw new Exception("HTTP token rotation discarded unrelated browser authentication");
                // A new explicit login must replace the old profile's authentication.
                credential.Cookie = Cookies.Create("account_id=123; cookie_token=explicit-login").Cookie;
                OfficialBrowserSession.UseCredential(credential, app.Store);
                await OfficialBrowserSession.PrepareAsync(observed, credential, app.Store, CancellationToken.None);
                if (!(await observed.CookieManager.GetCookiesAsync(MiyousheClient.NoteEndpoint))
                    .Any(c => c.Name == "cookie_token" && c.Value == "explicit-login"))
                    throw new Exception("Explicit login did not replace stale authentication");
                await OfficialBrowserSession.SaveAsync(observed, credential, app.Store, CancellationToken.None);
            }
            foreach (var scenario in new[] { "challenge", "wrong-role", "timeout", "cancel" })
            {
                using var browser = new OfficialNoteBrowser(app.Store);
                using var cancel = new CancellationTokenSource();
                var blockedRequests = 0;
                try
                {
                    await browser.ReadForCheckAsync(credential, core =>
                    {
                        processes.Add(core.BrowserProcessId);
                        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All,
                            CoreWebView2WebResourceRequestSourceKinds.All);
                        core.WebResourceRequested += (_, e) =>
                        {
                            var isNote = MiyousheClient.IsNoteEndpoint(new Uri(e.Request.Uri));
                            if (!isNote && e.ResourceContext != CoreWebView2WebResourceContext.Document)
                            {
                                e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(), 204, "No Content", "");
                                return;
                            }
                            blockedRequests++;
                            var target = scenario == "wrong-role" ? request.Replace("role_id=100", "role_id=101") : request;
                            var html = "<!doctype html><link rel=\"icon\" href=\"data:,\"><body>" +
                                (scenario is "timeout" or "cancel" ? "等待人工登录" :
                                "<script>location.href=" + JsonSerializer.Serialize(target) + ";</script>") + "</body>";
                            var body = isNote ? scenario == "challenge" ? """{"retcode":10035,"data":null}""" : valid : html;
                            e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(body)),
                                200, "OK", "Content-Type: " + (isNote ? "application/json" : "text/html") + "\r\nAccess-Control-Allow-Origin: *");
                            if (scenario == "cancel") cancel.CancelAfter(100);
                        };
                    }, cancel.Token, scenario == "cancel" ? null : TimeSpan.FromSeconds(5));
                    throw new Exception("Unexpected success: " + scenario);
                }
                catch (ApiFailure failure) when (scenario != "cancel" && failure.Kind is FailureKind.Verification or FailureKind.Network) { }
                catch (OperationCanceledException) when (scenario == "cancel" && cancel.IsCancellationRequested) { }
                if (blockedRequests is < 1 or > 2) throw new Exception("Unexpected request burst: " + scenario);
            }
            if (app.Windows.Cast<System.Windows.Window>().Any(w => w.IsVisible))
                throw new Exception("A WPF window became visible");
            if (stoleFocus) throw new Exception("The application or its browser took foreground focus");
            app.Store.Write("background-smoke-result.json", new
            {
                passed = true, invisibleMessageWindow = true, foregroundNotStolen = true, popupsBlocked = true,
                scriptDialogsSuppressed = true, controllerReused = true, boundedRequests = true,
                challengeReported = true, wrongRoleRejected = true, timeoutHandled = true, cancellationHandled = true,
                sessionCookiesRestored = true, rotatedCredentialsPersisted = true, encryptedSession = true,
                lazyProfileRecovery = true, explicitLoginReplacesStaleCookies = true,
                coldBootSessionRestored = coldBoot && coldBootSessionSent,
                authenticatedRequests = 0
            });
        }
        catch (Exception error)
        {
            app.Store.Write("background-smoke-result.json", new { passed = false, error = error.ToString() });
        }
        finally { focusCheck.Stop(); }
        app.Quit();
    }
}
