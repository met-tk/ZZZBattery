using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

public partial class SettingsWindow : Window
{
    private const string AuthorUrl = "https://space.bilibili.com/24340298";
    private bool _ready;
    private readonly ReminderSoundPlayer _previewSound = new();
    public SettingsWindow()
    {
        InitializeComponent();
        AdaptiveWindow.Attach(this, 380, 300);
        VersionText.Text = "v" + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.4.2");
        RefreshSettings();
        AppearanceTab.IsChecked = true;
        Activated += (_, _) => RefreshSettings();
        StateChanged += (_, _) => UpdateChromeState();
        Closed += (_, _) => _previewSound.Dispose();
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
        UpdateChromeState();
    }

    private void ChromeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && ResizeMode == ResizeMode.CanResize)
        {
            ToggleMaximize(); e.Handled = true; return;
        }
        if (e.LeftButton != MouseButtonState.Pressed) return;
        try { DragMove(); } catch (InvalidOperationException) { }
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeClick(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void CloseWindowClick(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void UpdateChromeState()
    {
        if (MaximizeGlyph is null) return;
        MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "❐" : "□";
        MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "还原" : "最大化";
        AutomationProperties.SetName(MaximizeButton, WindowState == WindowState.Maximized ? "还原" : "最大化");
    }

    private void RefreshSettings()
    {
        _ready = false;
        var settings = App.Current.Settings;
        ThemeBox.SelectedValue = settings.Theme; AccentBox.SelectedValue = settings.Accent;
        CharacterMode.IsChecked = settings.CharacterStyle && !settings.Mini;
        ClassicMode.IsChecked = !settings.CharacterStyle && !settings.Mini;
        MiniMode.IsChecked = settings.Mini;
        TopmostBox.IsChecked = settings.Topmost; LockedBox.IsChecked = settings.Locked;
        ThroughBox.IsChecked = settings.ClickThrough; StartupBox.IsChecked = settings.StartAtLogin;
        ReminderBox.IsChecked = settings.ReminderEnabled;
        MinutesBox.SelectedValue = settings.ReminderMinutes.ToString();
        EnergyReminderBox.IsChecked = settings.EnergyReminderEnabled;
        EnergyTargetBox.Text = settings.ReminderEnergy.ToString();
        SoundBox.SelectedValue = settings.ReminderSound;
        RefreshSound();
        WallpaperName.Text = App.Current.Wallpapers.Resolve(settings.Wallpaper).Name;
        GamePathText.Text = string.IsNullOrWhiteSpace(settings.GameExecutable) ? "尚未设置" : Path.GetFileName(settings.GameExecutable);
        GamePathText.ToolTip = string.IsNullOrWhiteSpace(settings.GameExecutable) ? null : settings.GameExecutable;
        var credential = App.Current.Session.Credential;
        var expired = App.Current.Session.Failure?.Kind == FailureKind.Login;
        AccountHeading.Text = credential is null ? "尚未连接" : expired ? "需要重新连接" : credential.Role is null ? "请选择游戏角色" : "已连接米游社";
        AccountText.Text = credential?.Role?.DisplayName ?? "连接后同步你的电量与游戏便笺";
        if (expired) AccountText.Text += "\n登录已失效，请重新连接米游社。";
        ConnectButton.Content = credential is null ? "连接米游社" : "连接 / 切换账号";
        SignOutButton.IsEnabled = credential is not null;
        _ready = true;
    }

    private void SectionChanged(object sender, RoutedEventArgs e)
    {
        if (PageScroll is null || sender is not RadioButton tab) return;
        var section = tab.Tag?.ToString();
        AppearancePage.Visibility = section == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
        DesktopPage.Visibility = section == "Desktop" ? Visibility.Visible : Visibility.Collapsed;
        RemindersPage.Visibility = section == "Reminders" ? Visibility.Visible : Visibility.Collapsed;
        AccountPage.Visibility = section == "Account" ? Visibility.Visible : Visibility.Collapsed;
        PageScroll.ScrollToTop();
    }

    private void ModeChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not RadioButton { IsChecked: true } mode) return;
        var settings = App.Current.Settings;
        settings.Mini = mode == MiniMode;
        // The mini bar remembers the underlying full-size layout for the tray's restore action.
        if (!settings.Mini) settings.CharacterStyle = mode == CharacterMode;
        Save();
    }

    private void Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var settings = App.Current.Settings;
        settings.Theme = ThemeBox.SelectedValue?.ToString() ?? "Dark";
        settings.Accent = AccentBox.SelectedValue?.ToString() ?? "Lime";
        settings.Topmost = TopmostBox.IsChecked == true;
        settings.Locked = LockedBox.IsChecked == true; settings.ClickThrough = ThroughBox.IsChecked == true;
        settings.ReminderEnabled = ReminderBox.IsChecked == true;
        settings.EnergyReminderEnabled = EnergyReminderBox.IsChecked == true;
        if (int.TryParse(MinutesBox.SelectedValue?.ToString(), out var minutes)) settings.ReminderMinutes = minutes;
        Save();
    }

    private void EnergyTargetChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        if (!int.TryParse(EnergyTargetBox.Text, out var target) || target is < 1 or > 240)
        { SetStatus("请输入 1～240 的整数电量；当前输入尚未保存。", true); return; }
        if (App.Current.Settings.ReminderEnergy == target) return;
        App.Current.Settings.ReminderEnergy = target;
        Save();
    }

    private void EnergyTargetLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (int.TryParse(EnergyTargetBox.Text, out var target) && target is >= 1 and <= 240) return;
        _ready = false; EnergyTargetBox.Text = App.Current.Settings.ReminderEnergy.ToString(); _ready = true;
        SetStatus("电量范围为 1～240，已恢复为上次保存的值。", true);
    }

    private void RefreshSound()
    {
        var settings = App.Current.Settings;
        CustomSoundPanel.Visibility = settings.ReminderSound == "Custom" ? Visibility.Visible : Visibility.Collapsed;
        SoundPathText.Text = string.IsNullOrWhiteSpace(settings.ReminderSoundPath) ? "尚未选择提示音" : Path.GetFileName(settings.ReminderSoundPath);
        SoundPathText.ToolTip = settings.ReminderSoundPath;
        PreviewSoundButton.IsEnabled = settings.ReminderSound != "Silent";
    }

    private void SoundChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        _previewSound.Stop();
        App.Current.Settings.ReminderSound = SoundBox.SelectedValue?.ToString() ?? "System";
        RefreshSound(); Save();
        if (App.Current.Settings.ReminderSound == "Custom" && string.IsNullOrWhiteSpace(App.Current.Settings.ReminderSoundPath))
            SetStatus("请选择 WAV 文件；选择前将使用系统声音。");
    }

    private void ChooseSoundClick(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "选择电量提醒提示音", Filter = "WAV 提示音 (*.wav)|*.wav", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            using var sound = ReminderSoundPlayer.Load(picker.FileName);
            App.Current.Settings.ReminderSoundPath = picker.FileName;
            RefreshSound(); Save();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or TimeoutException)
        { SetStatus("无法读取此提示音，请选择不超过 10 MB 的 PCM WAV 文件。", true); }
    }

    private void PreviewSoundClick(object sender, RoutedEventArgs e)
    {
        _previewSound.Stop();
        var settings = App.Current.Settings;
        if (settings.ReminderSound == "Silent") return;
        if (settings.ReminderSound == "Custom" && !_previewSound.TryPlay(settings.ReminderSoundPath))
        { SetStatus("提示音无法播放，请重新选择有效的 PCM WAV 文件。", true); return; }
        if (settings.ReminderSound == "System") SystemSounds.Exclamation.Play();
        SetStatus("正在试听；关闭设置窗口可停止自定义声音。");
    }

    private void TestReminderClick(object sender, RoutedEventArgs e)
    {
        _previewSound.Stop();
        var sent = App.Current.ShowReminder("这是一条测试通知，正式提醒将使用当前选择的提示音。");
        SetStatus(sent ? "测试通知已发送；若未显示，请检查 Windows 通知和勿扰设置。" : "通知未能发送，请稍后重试。", !sent);
    }

    private void Save()
    {
        var saved = App.Current.SaveSettings();
        SetStatus(saved ? "已保存 · 即时生效" : "已应用，但保存失败。请检查数据目录权限。", !saved);
    }

    private void SetStatus(string message, bool warning = false)
    {
        Status.Text = message;
        Status.SetResourceReference(TextBlock.ForegroundProperty, warning ? "Warning" : "Muted");
    }

    private void StartupChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        try
        {
            DesktopIntegration.Startup(StartupBox.IsChecked == true);
            App.Current.Settings.StartAtLogin = StartupBox.IsChecked == true;
            Save();
        }
        catch
        {
            _ready = false; StartupBox.IsChecked = App.Current.Settings.StartAtLogin; _ready = true;
            SetStatus("无法更新自动启动，请检查 Windows 设置。", true);
        }
    }
    private void ConnectClick(object sender, RoutedEventArgs e) => App.Current.OpenAccount();
    private void WallpapersClick(object sender, RoutedEventArgs e) => App.Current.OpenWallpapers();
    private void ChooseGameClick(object sender, RoutedEventArgs e)
    {
        App.Current.ChooseGame();
        RefreshSettings();
    }
    private void SignOutClick(object sender, RoutedEventArgs e)
    {
        try { App.Current.SignOut(); RefreshSettings(); SetStatus("已退出登录，并清除本机账号缓存。"); }
        catch { SetStatus("清除失败，请检查数据目录权限。", true); }
    }
    private void AuthorClick(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(AuthorUrl) { UseShellExecute = true }); }
        catch { }
    }
    private void DoneClick(object sender, RoutedEventArgs e) => Close();
}

