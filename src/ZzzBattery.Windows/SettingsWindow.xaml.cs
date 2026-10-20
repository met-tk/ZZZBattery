using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

public partial class SettingsWindow : Window
{
    private bool _ready;
    public SettingsWindow()
    {
        InitializeComponent();
        Width = Math.Min(560, Math.Max(320, SystemParameters.WorkArea.Width - 40));
        Height = Math.Min(680, Math.Max(300, SystemParameters.WorkArea.Height - 40));
        MinWidth = Math.Min(440, Width); MinHeight = Math.Min(360, Height);
        VersionText.Text = "v" + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.2.6");
        RefreshSettings();
        AppearanceTab.IsChecked = true;
        Activated += (_, _) => RefreshSettings();
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
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
        if (int.TryParse(MinutesBox.SelectedValue?.ToString(), out var minutes)) settings.ReminderMinutes = minutes;
        Save();
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
    private void DoneClick(object sender, RoutedEventArgs e) => Close();
}

