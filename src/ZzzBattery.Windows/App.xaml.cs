using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using ZzzBattery.Core;
using Forms = System.Windows.Forms;

namespace ZzzBattery.Windows;

public partial class App : Application
{
    public new static App Current => (App)Application.Current;
    public LocalStore Store { get; private set; } = null!;
    public WidgetSettings Settings { get; private set; } = null!;
    public WidgetSession Session { get; private set; } = null!;
    public MiyousheClient Client { get; private set; } = null!;
    public DailySignSession DailySign { get; private set; } = null!;
    public WallpaperService Wallpapers { get; private set; } = null!;
    public bool Demo { get; set; }
    public bool Exiting { get; private set; }
    public EnergySnapshot DemoSnapshot { get; private set; } = null!;
    private HttpClient? _http;
    private Forms.NotifyIcon? _tray;
    private DispatcherTimer? _timer;
    private MainWindow _widget = null!;
    private AccountWindow? _account;
    private SettingsWindow? _settings;
    private VerificationWindow? _verification;
    private OfficialNoteBrowser? _officialNotes;
    private DailySignWindow? _dailySignWindow;
    private Forms.ToolStripMenuItem? _dailySignMenu;
    private DateTimeOffset _dailySignReadyAt;
    private WallpaperWindow? _wallpapers;
    private DateTimeOffset _nextWallpaperCheck;
    private Mutex? _mutex;
    private ReminderState _reminders = new();
    private bool _ticking;
    private Task? _smokeTask;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var rootIndex = Array.IndexOf(e.Args, "--data-dir");
            var uiSmoke = e.Args.Contains("--ui-smoke-test");
            var backgroundSmoke = e.Args.Contains("--background-smoke-test");
            var displaySmoke = e.Args.Contains("--display-smoke-test");
            var placementIndex = Array.IndexOf(e.Args, "--placement-smoke-test");
            var placementSmoke = placementIndex >= 0;
            var smoke = e.Args.Contains("--smoke-test") || uiSmoke || placementSmoke || displaySmoke || backgroundSmoke;
            if (smoke && (rootIndex < 0 || rootIndex + 1 >= e.Args.Length))
            {
                MessageBox.Show("运行检查请使用 --data-dir 指定独立测试目录。", "绝区零桌面电量");
                Shutdown(2); return;
            }
            var root = rootIndex >= 0 && rootIndex + 1 < e.Args.Length ? e.Args[rootIndex + 1] :
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZZZBattery");
            if (smoke && (Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).Equals(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZZZBattery"), StringComparison.OrdinalIgnoreCase) ||
                    File.Exists(Path.Combine(root, "credentials.dpapi"))))
            { MessageBox.Show("检查目录不能使用真实账号数据目录。", "绝区零桌面电量"); Shutdown(2); return; }
            var mutexName = "Local\\ZZZBattery_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root))))[..16];
            _mutex = new Mutex(true, mutexName, out var created);
            if (!created) { MessageBox.Show("程序已经在运行，请点击任务栏右下角的电池托盘图标。", "绝区零桌面电量"); Shutdown(); return; }
            Store = new LocalStore(root);
            Settings = Store.Read<WidgetSettings>("settings.json") ?? new();
            if (!Guid.TryParse(Settings.DeviceId, out _)) Settings.DeviceId = Guid.NewGuid().ToString();
            // Persist the device identity before the first network request, including
            // first launch where the user has not opened/saved the settings window.
            Store.Write("settings.json", Settings);
            Wallpapers = new WallpaperService(root);
            Settings.ClickThrough = false; // Never lock the user out after a crash or restart.
            Settings.ReminderMinutes = Math.Clamp(Settings.ReminderMinutes, 1, 120);
            Palette.Apply(Settings);
            _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.All })
            { Timeout = TimeSpan.FromSeconds(20) };
            Client = new MiyousheClient(_http, Settings.DeviceId, includeWidget: true);
            _officialNotes = new OfficialNoteBrowser(root);
            Session = new WidgetSession(Client, officialNoteReader: _officialNotes.ReadAsync);
            DailySign = new DailySignSession(Client);
            _dailySignReadyAt = DateTimeOffset.UtcNow.AddSeconds(15);
            Demo = e.Args.Contains("--demo");
            DemoSnapshot = DemoData.Create(DateTimeOffset.UtcNow);
            var credential = Demo || smoke ? null : Store.LoadCredential();
            Session.CheckpointChanged += checkpoint => Store.Write("sync-state.json", checkpoint);
            Session.SetCredential(credential, smoke ? null : Store.Read<EnergySnapshot>("snapshot.json"),
                smoke ? null : Store.Read<SyncCheckpoint>("sync-state.json"));
            DailySign.SetCredential(credential, smoke ? null : Store.Read<DailySignCheckpoint>("daily-sign.json"));
            // The session must know when its pre-POST checkpoint cannot be saved.
            DailySign.CheckpointChanged += checkpoint => Store.Write("daily-sign.json", checkpoint);
            DailySign.Changed += () => Dispatcher.Invoke(() => _widget?.Render());
            _reminders = Store.Read<ReminderState>("reminders.json") ?? new();
            Session.Synced += snapshot =>
            {
                SafeStore(() => Store.Write("snapshot.json", snapshot));
                if (snapshot.CommunitySigned is { } signed)
                    DailySign.ObserveSigned(signed, snapshot.ReceivedAt, snapshot.AccountKey, snapshot.RoleKey);
            };
            Session.Changed += () => Dispatcher.Invoke(() =>
            {
                if (DailySign.Credential?.AccountKey != Session.Credential?.AccountKey || DailySign.Credential?.Role?.Key != Session.Credential?.Role?.Key)
                    DailySign.SetCredential(Session.Credential, Store.Read<DailySignCheckpoint>("daily-sign.json"));
                _widget?.Render();
            });
            _widget = new MainWindow(); MainWindow = _widget;
            if (!backgroundSmoke) { BuildTray(); _widget.Show(); _widget.ApplySettings(); }
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Tick; if (!uiSmoke && !placementSmoke && !displaySmoke && !backgroundSmoke) _timer.Start();
            SystemEvents.PowerModeChanged += PowerChanged;
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            SystemEvents.TimeChanged += TimeChanged;
            SystemEvents.UserPreferenceChanged += ThemeChanged;
            DispatcherUnhandledException += (_, args) =>
            {
                args.Handled = true;
                MessageBox.Show("操作未完成。请重试；如仍有问题，请退出后重新启动。", "绝区零桌面电量");
            };
            if (!smoke && !Demo && Settings.StartAtLogin)
            {
                try { DesktopIntegration.RefreshExistingStartupPath(); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                { _widget.SetNotice("自动启动路径暂未更新，请在设置中重新开关“开机自动启动”。"); }
            }
            if (backgroundSmoke) _smokeTask = BackgroundSmokeCheck.RunAsync(this);
            else if (displaySmoke) _smokeTask = DisplaySmokeCheck.RunAsync(this, _widget);
            else if (placementSmoke) _smokeTask = PlacementSmokeCheck.RunAsync(this, _widget,
                placementIndex + 1 < e.Args.Length ? e.Args[placementIndex + 1] : "");
            else if (uiSmoke) _smokeTask = UiSmokeCheck.RunAsync(this, _widget);
            else if (smoke) _smokeTask = SmokeCheck.RunAsync(this, _widget);
            else if (e.Args.Contains("--connect")) OpenAccount();
            else if (e.Args.Contains("--settings")) OpenSettings();
            else if (e.Args.Contains("--verify")) _ = RefreshAndVerify();
            if (!smoke) _ = RefreshWallpapers();
        }
        catch (Exception error)
        {
            MessageBox.Show("程序无法启动：" + (error is IOException ? "无法访问数据目录。" : error.GetType().Name), "绝区零桌面电量");
            Shutdown(1);
        }
    }

    private void BuildTray()
    {
        var resource = GetResourceStream(new Uri("pack://application:,,,/Assets/battery.ico"));
        _tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(resource.Stream), Text = "绝区零桌面电量", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        void Add(string label, Action action) => menu.Items.Add(label, null, (_, _) => Dispatcher.Invoke(action));
        Add("显示 / 隐藏小组件", ToggleVisibility);
        Add("刷新电量", () => _ = Refresh());
        _dailySignMenu = new Forms.ToolStripMenuItem("今日签到", null, (_, _) => Dispatcher.Invoke(() => _ = SignToday()));
        menu.Items.Add(_dailySignMenu);
        Add("检查签到状态", () => _ = CheckDailySign());
        Add("打开官方签到页", OpenDailySignPage);
        Add("切换标准 / 迷你", () => { Settings.Mini = !Settings.Mini; SaveSettings(); });
        Add("解除鼠标穿透", () => { Settings.ClickThrough = false; Settings.Locked = false; SaveSettings(); ShowWidget(); });
        Add("连接 / 切换账号", OpenAccount);
        Add("恢复电量同步", OpenVerification);
        Add("选择壁纸", OpenWallpapers);
        Add("设置", OpenSettings);
        Add("找回窗口", ShowWidget);
        menu.Items.Add(new Forms.ToolStripSeparator());
        Add("退出", Quit);
        _tray.ContextMenuStrip = menu;
        _tray.MouseClick += (_, args) => { if (args.Button == Forms.MouseButtons.Left) Dispatcher.Invoke(ToggleVisibility); };
        _tray.BalloonTipClicked += (_, _) => Dispatcher.Invoke(ShowWidget);
    }
    private void ToggleVisibility() { if (_widget.IsVisible) _widget.Hide(); else ShowWidget(); }
    public void ShowWidget()
    {
        _widget.Show(); _widget.WindowState = WindowState.Normal;
        _widget.RestorePosition();
    }
    public void OpenAccount()
    {
        if (_account is not null) { _account.Activate(); return; }
        _account = new AccountWindow();
        _account.Closed += (_, _) => _account = null;
        _account.Show(); _account.Activate();
    }
    public void OpenSettings()
    {
        if (_settings is not null) { _settings.Activate(); return; }
        _settings = new SettingsWindow();
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show(); _settings.Activate();
    }
    public void OpenWallpapers()
    {
        if (_wallpapers is not null) { _wallpapers.Activate(); return; }
        _wallpapers = new WallpaperWindow();
        _wallpapers.Closed += (_, _) => _wallpapers = null;
        _wallpapers.Show(); _wallpapers.Activate();
    }
    public void UpdateWallpaper() => _ = _widget.ApplyWallpaperAsync();
    private async Task RefreshWallpapers()
    {
        _nextWallpaperCheck = DateTimeOffset.UtcNow.AddMinutes(30);
        try { await Wallpapers.Catalog.RefreshAsync(); if (!Exiting) UpdateWallpaper(); }
        catch (OperationCanceledException) { }
    }
    public void OpenCommunity()
    {
        try { Process.Start(new ProcessStartInfo("https://www.miyoushe.com/zzz/") { UseShellExecute = true }); }
        catch { _widget.SetNotice("无法打开系统浏览器。"); }
    }
    public void ChooseGame(bool launch = false)
    {
        var picker = new OpenFileDialog { Title = "选择绝区零游戏或启动器", Filter = "游戏或快捷方式|*.exe;*.lnk", CheckFileExists = true };
        if (picker.ShowDialog() != true) return;
        Settings.GameExecutable = picker.FileName; SaveSettings();
        if (launch) LaunchGame();
    }
    public void LaunchGame()
    {
        if (string.IsNullOrWhiteSpace(Settings.GameExecutable) || !File.Exists(Settings.GameExecutable)) { ChooseGame(true); return; }
        try { Process.Start(new ProcessStartInfo(Settings.GameExecutable) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(Settings.GameExecutable) ?? "" }); }
        catch { _widget.SetNotice("游戏启动失败，请在设置中重新选择游戏程序。"); }
    }
    public void OpenVerification()
    {
        if (Session.Credential?.Role is null) { OpenAccount(); return; }
        if (Session.RecoveringInBackground) { _widget.SetNotice("正在后台恢复同步，请稍候。"); return; }
        if (_verification is not null) { _verification.Activate(); return; }
        if (Session.Failure?.Kind != FailureKind.Verification &&
            !(Session.Failure?.Kind == FailureKind.Permission && Session.Failure.Code is 5003 or 10041))
        {
            ShowWidget();
            _widget.SetNotice(Session.Failure?.Kind == FailureKind.Login ? "登录已失效，请在账号设置中重新连接米游社。" : "当前没有待处理的电量验证。");
            return;
        }
        _verification = new VerificationWindow(Session.Credential, Session.Failure);
        _verification.Closed += (_, _) => _verification = null;
        _verification.Show(); _verification.Activate();
    }
    private async Task RefreshAndVerify()
    {
        await Refresh();
        if (Session.Failure?.Kind == FailureKind.Verification) OpenVerification();
    }
    public void OpenOfficialRecord()
    {
        try { Process.Start(new ProcessStartInfo(MiyousheClient.VerificationPageUrl(Session.Credential?.Role)) { UseShellExecute = true }); }
        catch { _widget.SetNotice("无法打开系统浏览器，请从米游社进入绝区零战绩页。"); }
    }
    public async Task Connect(Credential credential)
    {
        _verification?.Close(); _dailySignWindow?.Close(); Client.ForgetVerification();
        Store.SaveCredential(credential);
        Demo = false; _reminders = new();
        Session.SetCredential(credential);
        _officialNotes?.Reset();
        var signCheckpoint = Store.Read<DailySignCheckpoint>("daily-sign.json");
        if (signCheckpoint?.FailureKind == FailureKind.Login)
            signCheckpoint = signCheckpoint with { FailureKind = null, FailureCode = null };
        DailySign.SetCredential(credential, signCheckpoint);
        await Refresh();
    }
    public void SignOut()
    {
        _verification?.Close(); _dailySignWindow?.Close(); Client.ForgetVerification();
        Store.ClearAccount(); Session.SetCredential(null); _reminders = new(); Demo = false;
        _officialNotes?.Reset();
        _account?.Close(); _widget.Render();
    }
    public async Task SignToday()
    {
        if (Demo) { _widget.SetNotice("当前为演示界面；连接账号后可签到。"); return; }
        if (Session.Credential?.Role is null || DailySign.Failure?.Kind == FailureKind.Login) { OpenAccount(); return; }
        if (DailySign.IsSignedToday || DailySign.Busy) return;
        if (DailySign.Failure?.Kind is FailureKind.Verification or FailureKind.Permission) { OpenDailySignPage(); return; }
        await DailySign.SignAsync();
        _widget.SetNotice(DailySign.Message);
        if (DailySign.Failure?.Kind == FailureKind.Verification) OpenDailySignPage();
    }
    public async Task CheckDailySign()
    {
        if (Demo || Session.Credential?.Role is null) { if (!Demo) OpenAccount(); return; }
        await DailySign.CheckAsync(manual: true);
        _widget.SetNotice(DailySign.Message);
    }
    public void OpenDailySignPage()
    {
        if (Demo || Session.Credential?.Role is null) { if (!Demo) OpenAccount(); return; }
        if (_dailySignWindow is not null) { _dailySignWindow.Activate(); return; }
        _dailySignWindow = new DailySignWindow(Session.Credential);
        _dailySignWindow.Closed += (_, _) => _dailySignWindow = null;
        _dailySignWindow.Show();
    }
    public bool SaveSettings()
    {
        var saved = SafeStore(() => Store.Write("settings.json", Settings));
        Palette.Apply(Settings); _widget.ApplySettings();
        return saved;
    }
    public void SavePosition() => SafeStore(() => Store.Write("settings.json", Settings));
    public async Task Refresh()
    {
        if (Demo) { _widget.Render(); return; }
        try
        {
            await Session.RefreshAsync(true);
            if (Session.Message.StartsWith("请在", StringComparison.Ordinal)) _widget.SetNotice(Session.Message);
        }
        catch { _widget.SetNotice("同步未完成，请稍后重试。"); }
    }
    private async void Tick(object? sender, EventArgs args)
    {
        if (_ticking || Exiting) return;
        _ticking = true;
        try
        {
            if (DateTimeOffset.UtcNow >= _nextWallpaperCheck) _ = RefreshWallpapers();
            if (!Demo) await Session.RefreshAsync();
            DailySign.Tick();
            if (!Demo && DateTimeOffset.UtcNow >= _dailySignReadyAt &&
                Session.Failure?.Kind is not (FailureKind.Login or FailureKind.Verification or FailureKind.Permission or FailureKind.RateLimited))
                await DailySign.CheckAsync();
            if (_widget.IsVisible) _widget.Render();
            if (_dailySignMenu is not null)
            {
                _dailySignMenu.Text = DailySign.ButtonText;
                _dailySignMenu.Enabled = !Demo && !DailySign.Busy && !DailySign.IsSignedToday;
            }
            var projection = Session.Projection();
            if (_tray is not null) _tray.Text = projection is null ? "绝区零 · 未连接账号" : $"绝区零电量 {projection.Current}/{projection.Maximum}";
            if (Settings.ReminderEnabled && Session.Snapshot is { } snapshot && projection is not null)
            {
                var message = _reminders.Evaluate(snapshot, projection, DateTimeOffset.UtcNow,
                    !Demo && Session.Failure is null && !Session.ClockUncertain, Settings.ReminderMinutes);
                if (message is not null)
                {
                    _tray?.ShowBalloonTip(6000, "绝区零 · 电量提醒", message, Forms.ToolTipIcon.Info);
                    SafeStore(() => Store.Write("reminders.json", _reminders));
                }
            }
        }
        catch { _widget.SetNotice("暂时无法更新，请稍后刷新。"); }
        finally { _ticking = false; }
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) Dispatcher.BeginInvoke(() => { Session.InvalidateTime(); DailySign.InvalidateTime(); _widget.RestorePosition(); });
    }
    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => _widget.RestorePosition());
    private void TimeChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => { Session.InvalidateTime(); DailySign.InvalidateTime(); });
    private void ThemeChanged(object sender, UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(() => { Palette.Apply(Settings); _widget.Render(); });
    private bool SafeStore(Action action)
    {
        try { action(); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _widget?.SetNotice("数据目录无法写入，本次设置尚未保存。"); return false; }
    }
    public void Quit() { _widget?.FlushUserPosition(); Exiting = true; Shutdown(); }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _widget?.FlushUserPosition();
        base.OnSessionEnding(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        Exiting = true; _timer?.Stop(); DailySign?.Dispose(); Session?.Dispose(); _officialNotes?.Dispose(); _http?.Dispose(); Wallpapers?.Dispose();
        if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        SystemEvents.PowerModeChanged -= PowerChanged;
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        SystemEvents.TimeChanged -= TimeChanged;
        SystemEvents.UserPreferenceChanged -= ThemeChanged;
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
