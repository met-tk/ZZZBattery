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
            var credential = Cookies.Create("account_id=123; cookie_token=background-test-only");
            credential.Role = new("100", "prod_gf_cn", "测试", "测试");
            var request = MiyousheClient.NoteEndpoint + "?role_id=100&server=prod_gf_cn";
            var valid = """{"retcode":0,"data":{"energy":{"progress":{"current":119,"max":240},"restore":21780}}}""";
            var requests = 0; var pages = 0; var popups = 0; var controllers = 0;
            using (var browser = new OfficialNoteBrowser(app.Store.Root))
            {
                void Configure(CoreWebView2 core)
                {
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
                        if (note) requests++; else pages++;
                        var body = note ? valid : "<!doctype html><link rel=\"icon\" href=\"data:,\"><body>后台读取测试<script>" +
                            "alert('must stay silent');window.open('https://example.com/blocked');" +
                            "location.href=" + JsonSerializer.Serialize(request) + ";</script></body>";
                        e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(body)),
                            200, "OK", "Content-Type: " + (note ? "application/json" : "text/html; charset=utf-8") +
                            "\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-store");
                    };
                }
                var first = await browser.ReadForCheckAsync(credential, Configure);
                if (first.Current != 119 || first.Source != "官方网页便笺" || !browser.InvisibleForCheck)
                    throw new Exception("Invisible response capture failed");
                var second = await browser.ReadForCheckAsync(credential, _ => { });
                if (second.Current != 119 || requests != 2 || pages != 2 || controllers != 1 || popups != 2)
                    throw new Exception($"Unexpected requests/lifetime: notes={requests}, pages={pages}, controllers={controllers}, popups={popups}");
            }
            foreach (var scenario in new[] { "challenge", "wrong-role", "timeout", "cancel" })
            {
                using var browser = new OfficialNoteBrowser(app.Store.Root);
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
                    }, cancel.Token, scenario is "timeout" or "challenge" ? TimeSpan.FromSeconds(3) : null);
                    throw new Exception("Unexpected success: " + scenario);
                }
                catch (ApiFailure failure) when (scenario != "cancel" && failure.Kind == FailureKind.Verification) { }
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
                genuineChallengePaused = true, wrongRoleRejected = true, timeoutHandled = true, cancellationHandled = true,
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
