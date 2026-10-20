using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

internal static class UiSmokeCheck
{
    public static async Task RunAsync(App app, MainWindow window)
    {
        try
        {
            app.Settings.CharacterStyle = true; app.Settings.Mini = false; app.Settings.Expanded = false;
            app.Demo = false; window.ApplySettings(); await window.ApplyWallpaperAsync();
            await Task.Delay(150); Capture(window, app, "first-run");
            app.Demo = true; window.Render(); window.UpdateLayout();
            AssertText(window.Character, "CoffeeMark", "✓");
            AssertText(window.Character, "VideoMark", "•");
            AssertText(window.Character, "ScratchMark", "!");
            Capture(window, app, "character"); Capture(window, app, "character-150", 1.5); Capture(window, app, "character-200", 2);
            var next = (Button)window.Character.FindName("NextPage");
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            AssertText(window.Character, "PageNumber", "2 / 4");
            Capture(window, app, "character-page2");
            ((Button)window.Character.FindName("AllNotes")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!app.Settings.Expanded) throw new Exception("Details button did not expand notes.");
            window.UpdateLayout(); Capture(window, app, "details");
            var notesScroll = window.CharacterNotes.ScrollForCheck;
            notesScroll.ApplyTemplate();
            var bar = (ScrollBar)notesScroll.Template.FindName("PART_VerticalScrollBar", notesScroll);
            bar.ApplyTemplate();
            if (bar.ActualWidth > 10.1) throw new Exception("Notes scrollbar still uses the wide system appearance.");
            notesScroll.ScrollToTop(); await Task.Delay(50);
            var track = (Track)bar.Template.FindName("PART_Track", bar);
            track.Thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            track.Thumb.RaiseEvent(new DragDeltaEventArgs(0, 35) { RoutedEvent = Thumb.DragDeltaEvent });
            track.Thumb.RaiseEvent(new DragCompletedEventArgs(0, 35, false) { RoutedEvent = Thumb.DragCompletedEvent });
            await Task.Delay(50);
            if (notesScroll.VerticalOffset <= 0) throw new Exception("Custom scrollbar thumb did not scroll notes.");
            notesScroll.ScrollToTop(); await Task.Delay(50);
            notesScroll.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.MouseWheelEvent });
            await Task.Delay(50);
            if (notesScroll.VerticalOffset <= 0) throw new Exception("Notes mouse wheel scrolling failed.");
            notesScroll.ScrollToTop();
            foreach (var filter in new[] { "待办", "活动", "经营", "每周", "全部" })
            { window.CharacterNotes.FilterForCheck(filter); window.UpdateLayout(); Capture(window, app, "details-" + filter); }
            var stale = NoteBoard.WithMissing(NoteBoard.Project(app.DemoSnapshot, DateTimeOffset.UtcNow.AddHours(3)));
            window.Character.SetData("90", "240", "上次电量 · 等待同步", stale, false);
            AssertText(window.Character, "CoffeeMark", "↻"); Capture(window, app, "stale");
            app.Settings.Theme = "Light"; Palette.Apply(app.Settings); window.Render(); Capture(window, app, "details-light");
            app.Settings.CharacterStyle = false; window.ApplySettings(); Capture(window, app, "classic-light");
            app.Settings.Theme = "Dark"; Palette.Apply(app.Settings); window.ApplySettings(); Capture(window, app, "classic-dark");
            app.Settings.Mini = true; window.ApplySettings(); Capture(window, app, "mini");
            // A short work area must expose a scrollable viewport, not clip the controls.
            app.Settings.Mini = false; app.Settings.CharacterStyle = true; window.ApplySettings(); window.MaxHeight = 480;
            window.UpdateLayout(); Capture(window, app, "short-screen");
            window.WidgetScroll.ScrollToBottom(); window.UpdateLayout(); Capture(window, app, "short-screen-bottom");
            var card = window.Character;
            card.SetData("12345", "240", "已超过自然恢复上限", DemoData.Create(DateTimeOffset.UtcNow).Notes, false);
            Capture(card, Path.Combine(app.Store.Root, "large-energy.png"));
            var qr = new QrLoginWindow(preview: true) { Owner = window };
            qr.Show(); await Task.Delay(100); Capture(qr, app, "qr-login-preview"); qr.Close();
            // Exercise the native failure panel without network or real credentials.
            var verification = new VerificationWindow(new Credential(), null, loadPage: false);
            verification.Show(); verification.ShowFailureForCheck(); verification.UpdateLayout();
            Capture(verification, app, "verification-unavailable"); verification.Close();
            await CheckSettings(app);
            await CheckDailySign(app, window);
            await CheckVerificationRecovery(app);
            File.WriteAllText(Path.Combine(app.Store.Root, "ui-smoke-result.json"), JsonSerializer.Serialize(new
            { passed = true, states = true, pagination = true, details = true, filters = true, dpi = new[] { 100, 150, 200 }, stale = true, shortScreen = true, qrLogin = true,
                slimScrollbar = true, thumbDrag = true, mouseWheel = true, verificationFailurePanel = true,
                dailySign = new { headerLayout = true, states = true, clickDispatch = true, signedDisabled = true, officialQr = true, accountIsolation = true, noSubmission = true },
                settings = new { navigation = true, modes = true, toggles = true, persistence = true, themedDropdown = true, reminderDependency = true, shortWindow = true },
                recovery = new { capturedNote = true, wrongRoleRejected = true, malformedResponseRejected = true, staleCacheRejected = true, ordinarySyncClosesWindow = true, automaticClose = true, webViewResponsePipeline = true } }));
        }
        catch (Exception error)
        { File.WriteAllText(Path.Combine(app.Store.Root, "ui-smoke-result.json"), JsonSerializer.Serialize(new { passed = false, error = error.ToString() })); }
        finally { app.Quit(); }
    }
    private static async Task CheckDailySign(App app, MainWindow window)
    {
        static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        var preview = new CharacterCard();
        preview.Measure(new Size(360, 420)); preview.Arrange(new Rect(0, 0, 360, 420)); preview.UpdateLayout();
        var button = (Button)preview.FindName("DailySignButton");
        var refresh = (Button)preview.FindName("RefreshButton");
        var requests = 0; preview.DailySignRequested += (_, _) => requests++;
        foreach (var text in new[] { "今日签到", "签到中…", "查询中…", "需验证", "稍后重试", "待确认", "请登录", "已签到" })
        {
            preview.SetDailySign(text, true, "界面检查"); preview.UpdateLayout();
            Assert((string)button.Content == text && button.ActualWidth >= 64, "Sign label does not fit.");
            var right = button.TransformToAncestor(preview).Transform(new Point(button.ActualWidth, 0)).X;
            var nextLeft = refresh.TransformToAncestor(preview).Transform(new Point(0, 0)).X;
            Assert(right < nextLeft && right < 360, "Sign button overlaps refresh.");
            if (text == "已签到") Assert(!button.IsEnabled, "Signed button must be disabled.");
        }
        preview.SetDailySign("今日签到", true, "点击签到");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(requests == 1, "Sign click was not dispatched exactly once.");
        Capture(preview, Path.Combine(app.Store.Root, "daily-sign-button.png"));
        preview.SetDailySign("已签到", false, "今日已签到");
        Capture(preview, Path.Combine(app.Store.Root, "daily-sign-button-complete.png"));

        app.Demo = false;
        var credential = new Credential { AccountKey = "ui-sign", Role = new("100", "prod_gf_cn", "演示角色", "国服") };
        app.Session.SetCredential(credential);
        var now = DateTimeOffset.UtcNow;
        app.DailySign.SetCredential(credential, new(credential.AccountKey, credential.Role.Key, now,
            RetryAt: now.AddMinutes(15), NextCheckAt: now.AddMinutes(15), FailureKind: FailureKind.Verification, FailureCode: 10035));
        var signWindow = new DailySignWindow(credential); signWindow.Show(); await Task.Delay(80);
        Assert(((Image)signWindow.FindName("QrImage")).Source is not null, "Official sign QR missing.");
        Assert(!((Button)signWindow.FindName("CheckButton")).IsEnabled, "Cooldown was discarded.");
        Capture(signWindow, app, "daily-sign-official");
        app.DailySign.ObserveSigned(true, DateTimeOffset.UtcNow, credential.AccountKey, credential.Role.Key);
        await Task.Delay(80); window.Render(); signWindow.UpdateLayout();
        Assert((string)((Button)window.Character.FindName("DailySignButton")).Content == "已签到", "Confirmed sign not rendered.");
        Assert(((TextBlock)signWindow.FindName("StatusHeading")).Text == "今日已签到", "Sign recovery window did not reflect confirmation.");
        Capture(signWindow, app, "daily-sign-confirmed");
        app.Session.SetCredential(new Credential { AccountKey = "other", Role = credential.Role });
        await Task.Delay(80);
        Assert(!((Button)signWindow.FindName("CheckButton")).IsEnabled, "Old role window is still actionable.");
        Assert(((TextBlock)signWindow.FindName("StatusHeading")).Text == "账号或角色已切换", "Old role window did not invalidate.");
        signWindow.Close();
    }
    private static async Task CheckVerificationRecovery(App app)
    {
        app.Demo = false;
        var credential = new Credential { AccountKey = "ui-test", Role = new("100", "prod_gf_cn", "演示角色", "国服") };
        var old = new EnergySnapshot(credential.AccountKey, credential.Role.Key, 90, 240, 27000, DateTimeOffset.UtcNow.AddMinutes(-5), [], SchemaVersion: 1);
        SyncCheckpoint Checkpoint() => new(credential.AccountKey, credential.Role.Key, DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(1), FailureKind.Verification, 10035, 1, VerificationPath: "/api/zzz/widget");
        app.Session.SetCredential(credential, old, Checkpoint());
        var verification = new VerificationWindow(credential, app.Session.Failure, loadPage: false);
        verification.Show(); await Task.Delay(50);
        const string json = """{"retcode":0,"data":{"energy":{"progress":{"current":119,"max":240},"restore":21780}}}""";
        verification.CaptureNoteForCheck(new Uri(MiyousheClient.NoteEndpoint + "?role_id=101&server=prod_gf_cn"), 200, json);
        if (verification.RecoveryCompletedForCheck || app.Session.Snapshot != old) throw new Exception("Wrong-role page completed recovery.");
        var uri = new Uri(MiyousheClient.NoteEndpoint + "?role_id=100&server=prod_gf_cn");
        verification.CaptureNoteForCheck(uri, 200, "{\"retcode\":0,\"data\":{}}");
        if (verification.RecoveryCompletedForCheck) throw new Exception("Incomplete page completed recovery.");
        verification.CaptureNoteForCheck(uri, 200, json);
        if (!verification.RecoveryCompletedForCheck || app.Session.Snapshot is not { Current: 119, Source: "官方网页便笺" }) throw new Exception("Readable note was not immediately synchronized.");
        await Task.Delay(100); Capture(verification, app, "verification-web-synced"); verification.Close();
        app.Session.SetCredential(credential, old, Checkpoint());
        verification = new VerificationWindow(credential, app.Session.Failure, loadPage: false);
        verification.Show();
        app.Session.ResumeAfterVerification(resetCooldown: true);
        if (verification.RecoveryCompletedForCheck) throw new Exception("Clearing a failure promoted old cached data.");
        app.Session.ResumeAfterVerification(old with { ReceivedAt = DateTimeOffset.UtcNow, Current = 120 });
        if (!verification.RecoveryCompletedForCheck) throw new Exception("Ordinary successful sync left recovery waiting.");
        await Task.Delay(100); Capture(verification, app, "verification-sync-recovered");
        await Task.Delay(3100);
        if (verification.IsVisible) throw new Exception("Recovery window did not automatically close.");
        app.Session.SetCredential(credential, old, Checkpoint());
        verification = new VerificationWindow(credential, app.Session.Failure, loadPage: false);
        verification.Show();
        // All test requests are fulfilled in-process; no real account or API call.
        await verification.ExerciseResponsePipelineForCheck(json);
        Capture(verification, app, "verification-webview-response"); verification.Close();
        app.Session.SetCredential(null);
    }

    private static async Task CheckSettings(App app)
    {
        // Only the isolated smoke-test profile is changed. Never exercise Windows auto-start here.
        app.Settings.CharacterStyle = true; app.Settings.Mini = false;
        app.Settings.Theme = "Dark"; app.Settings.Accent = "Lime";
        app.Settings.Topmost = false; app.Settings.Locked = false; app.Settings.ClickThrough = false;
        app.Settings.ReminderEnabled = false;
        app.Settings.GameExecutable = @"C:\Games\HoYoPlay\games\ZenlessZoneZero\launcher.exe";
        Palette.Apply(app.Settings);
        var settings = new SettingsWindow(); settings.Show(); await Task.Delay(100);
        T Control<T>(string name) where T : FrameworkElement => (T)settings.FindName(name);
        void Select(string name) => ((ISelectionItemProvider)new RadioButtonAutomationPeer(Control<RadioButton>(name)).GetPattern(PatternInterface.SelectionItem)).Select();
        void Toggle(string name) => ((IToggleProvider)new CheckBoxAutomationPeer(Control<CheckBox>(name)).GetPattern(PatternInterface.Toggle)).Toggle();
        void Assert(bool condition, string message) { if (!condition) throw new Exception("Settings: " + message); }
        var scroll = Control<ScrollViewer>("PageScroll");
        Assert(settings.Height < 670 || scroll.ScrollableHeight <= 1, "Appearance page does not fit the default window.");
        Capture(settings, app, "settings-appearance");
        Capture(settings, app, "settings-appearance-150", 1.5); Capture(settings, app, "settings-appearance-200", 2);
        Select("ClassicMode"); Assert(!app.Settings.Mini && !app.Settings.CharacterStyle, "Classic mode did not apply.");
        Select("MiniMode"); Assert(app.Settings.Mini && !app.Settings.CharacterStyle, "Mini mode lost the previous layout.");
        Select("CharacterMode"); Assert(!app.Settings.Mini && app.Settings.CharacterStyle, "Character mode did not apply.");
        var theme = Control<ComboBox>("ThemeBox");
        theme.IsDropDownOpen = true; await Task.Delay(150);
        var popup = (Popup)theme.Template.FindName("PART_Popup", theme);
        Assert(popup.IsOpen && popup.Child is FrameworkElement { ActualWidth: > 100 }, "Theme dropdown did not render.");
        Capture((FrameworkElement)popup.Child, Path.Combine(app.Store.Root, "settings-dropdown-dark.png"));
        theme.IsDropDownOpen = false;
        theme.SelectedValue = "Light"; Control<ComboBox>("AccentBox").SelectedValue = "Yellow";
        await Task.Delay(100);
        Capture(settings, app, "settings-appearance-light");
        theme.IsDropDownOpen = true; await Task.Delay(100);
        Capture((FrameworkElement)popup.Child, Path.Combine(app.Store.Root, "settings-dropdown-light.png"));
        theme.IsDropDownOpen = false;
        theme.SelectedValue = "Dark"; Control<ComboBox>("AccentBox").SelectedValue = "Lime";
        Select("DesktopTab");
        Assert(Control<StackPanel>("DesktopPage").IsVisible && !Control<StackPanel>("AppearancePage").IsVisible, "Page navigation failed.");
        Toggle("TopmostBox"); Toggle("LockedBox"); Toggle("ThroughBox");
        Assert(app.Settings.Topmost && app.Settings.Locked && app.Settings.ClickThrough, "Desktop toggles did not apply.");
        Toggle("ThroughBox"); Toggle("TopmostBox");
        settings.Activate(); await Task.Delay(100); settings.UpdateLayout();
        Assert(settings.Height < 670 || scroll.ScrollableHeight <= 1, "Desktop page does not fit the default window.");
        Capture(settings, app, "settings-desktop");
        Select("ReminderTab");
        var minutes = Control<ComboBox>("MinutesBox");
        Assert(!minutes.IsEnabled, "Reminder time should be disabled when reminders are off.");
        Toggle("ReminderBox"); Assert(minutes.IsEnabled, "Reminder time did not enable.");
        minutes.SelectedValue = "15";
        Toggle("ReminderBox"); Assert(!minutes.IsEnabled && app.Settings.ReminderMinutes == 15, "Disabling reminders lost the time.");
        Toggle("ReminderBox"); await Task.Delay(100); Capture(settings, app, "settings-reminders");
        Select("AccountTab"); await Task.Delay(100); Capture(settings, app, "settings-account");
        Assert(!Control<Button>("SignOutButton").IsEnabled, "Signed-out account allows sign-out.");
        settings.Width = 440; settings.Height = 480; Select("DesktopTab"); settings.UpdateLayout();
        Assert(scroll.ScrollableHeight > 0, "Small window has no scrolling.");
        scroll.ScrollToBottom(); await Task.Delay(50);
        Assert(scroll.VerticalOffset > 0, "Small window could not reach the lower settings.");
        var done = Control<Button>("DoneButton");
        Assert(done.TranslatePoint(new Point(0, done.ActualHeight), settings).Y <= settings.ActualHeight, "Fixed footer was clipped.");
        Capture(settings, app, "settings-short-desktop");
        Select("AppearanceTab"); await Task.Delay(50);
        Assert(scroll.VerticalOffset == 0, "Page switch did not reset scrolling.");
        Capture(settings, app, "settings-short-appearance");
        scroll.ScrollToBottom(); await Task.Delay(50); Capture(settings, app, "settings-short-appearance-bottom");
        settings.Close();
        var saved = app.Store.Read<WidgetSettings>("settings.json");
        Assert(saved is { CharacterStyle: true, Mini: false, Locked: true, Topmost: false, ClickThrough: false, ReminderEnabled: true, ReminderMinutes: 15, Theme: "Dark", Accent: "Lime" }, "Settings did not persist.");
        settings = new SettingsWindow(); settings.Show(); await Task.Delay(50);
        Assert(Control<RadioButton>("CharacterMode").IsChecked == true && Control<CheckBox>("LockedBox").IsChecked == true && Control<ComboBox>("MinutesBox").SelectedValue?.ToString() == "15", "Reopening lost saved selections.");
        settings.Close();
    }
    private static void AssertText(FrameworkElement control, string name, string expected)
    {
        if (((TextBlock)control.FindName(name)).Text != expected) throw new Exception($"Unexpected {name}.");
    }
    private static void Capture(Window window, App app, string name, double scale = 1) =>
        Capture(window, Path.Combine(app.Store.Root, name + ".png"), scale);
    private static void Capture(FrameworkElement element, string path, double scale = 1)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * scale), (int)Math.Ceiling(element.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path); encoder.Save(output);
    }
}
