using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

public partial class MainWindow : Window
{
    private readonly Border[] _segments = new Border[24];
    private readonly WindowPlacementController _placement;
    internal WindowPlacementController PlacementForCheck => _placement;
    private string? _notice;
    private DateTimeOffset _noticeUntil;

    private string? _artworkUrl;
    private ImageSource? _artwork;
    private int _artworkVersion;
    public MainWindow()
    {
        InitializeComponent();
        Character.LaunchRequested += (_, _) => App.Current.LaunchGame();
        Character.RefreshRequested += async (_, _) => await App.Current.Refresh();
        Character.DailySignRequested += async (_, _) => await App.Current.SignToday();
        Character.CommunityRequested += (_, _) => App.Current.OpenCommunity();
        Character.WallpaperRequested += (_, _) => App.Current.OpenWallpapers();
        Character.DetailsRequested += (_, _) => { App.Current.Settings.Expanded = !App.Current.Settings.Expanded; App.Current.SaveSettings(); };
        for (int i = 0; i < _segments.Length; i++)
        {
            _segments[i] = new Border { Margin = new Thickness(0, 0, 3, 0), CornerRadius = new CornerRadius(1) };
            EnergyMeter.Children.Add(_segments[i]);
        }
        var settings = App.Current.Settings;
        Left = settings.Left is { } x && double.IsFinite(x) ? x : SystemParameters.WorkArea.Right - Width - 28;
        Top = settings.Top is { } y && double.IsFinite(y) ? y : SystemParameters.WorkArea.Top + 70;
        _placement = new(this, settings);
        Loaded += (_, _) => { ApplySettings(); Render(); _placement.QueueRestore(); };
        Closing += (_, e) => { if (!App.Current.Exiting) { e.Cancel = true; Hide(); } };
    }
    public void ApplySettings()
    {
        var settings = App.Current.Settings;
        var character = settings.CharacterStyle && !settings.Mini;
        var preferredWidth = settings.Mini ? 304 : character ? 376 : 360;
        WidgetContent.Width = preferredWidth;
        Width = preferredWidth;
        CharacterShell.Visibility = character ? Visibility.Visible : Visibility.Collapsed;
        ClassicShell.Visibility = character ? Visibility.Collapsed : Visibility.Visible;
        Standard.Visibility = settings.Mini ? Visibility.Collapsed : Visibility.Visible;
        Mini.Visibility = settings.Mini ? Visibility.Visible : Visibility.Collapsed;
        Topmost = settings.Topmost;
        DesktopIntegration.ClickThrough(this, settings.ClickThrough);

        _ = ApplyWallpaperAsync();
        Render();
        _placement.QueueRestore();
    }
    public async Task ApplyWallpaperAsync()
    {
        var service = App.Current.Wallpapers;
        var wallpaper = service.Resolve(App.Current.Settings.Wallpaper);
        if (_artworkUrl == wallpaper.ImageUrl && _artwork is not null) { Character.SetTheme(wallpaper, _artwork); return; }
        var version = ++_artworkVersion;
        _artworkUrl = wallpaper.ImageUrl;
        _artwork = service.BundledImage(wallpaper.ImageUrl);
        Character.SetTheme(wallpaper, _artwork);
        var loaded = await service.GetImageAsync(wallpaper.ImageUrl);
        if (version != _artworkVersion || App.Current.Exiting) return;
        _artwork = loaded;
        Character.SetTheme(wallpaper, loaded);
        if (loaded is null) SetNotice("壁纸图片暂时无法加载，请联网后在“选择壁纸”中重试。");
    }
    public void SetNotice(string text) { _notice = text; _noticeUntil = DateTimeOffset.UtcNow.AddSeconds(8); Render(); }
    private Brush Brush(string key) => (Brush)FindResource(key);
    public void Render()
    {
        var app = App.Current;
        var session = app.Session;
        var signText = app.Demo ? "今日签到" : app.DailySign.ButtonText;
        var signEnabled = !app.Demo && !app.DailySign.Busy && !app.DailySign.IsSignedToday;
        var signHint = app.Demo ? "演示界面，不会发送签到请求。" : "绝区零米游社每日签到 · " + app.DailySign.Message;
        Character.SetDailySign(signText, signEnabled, signHint);
        DailySignButton.Content = signText; DailySignButton.IsEnabled = signEnabled; DailySignButton.ToolTip = signHint;
        var snapshot = app.Demo ? app.DemoSnapshot : session.Snapshot;
        var notes = snapshot is null ? null : NoteBoard.WithMissing(NoteBoard.Project(snapshot, DateTimeOffset.UtcNow,
            !app.Demo && (session.ClockUncertain || session.Failure is not null)));
        var projection = app.Demo ? EnergyEstimator.Project(app.DemoSnapshot, DateTimeOffset.UtcNow - app.DemoSnapshot.ReceivedAt) : session.Projection();
        EnergyNumber.Text = MiniNumber.Text = projection?.Current.ToString() ?? "—";
        EnergyMax.Text = MiniMax.Text = projection is null ? "/ —" : "/ " + projection.Maximum;
        EnergyNumber.FontSize = projection?.Current >= 1000 ? 52 : 70;
        EnergyLabel.Text = app.Demo ? "当前电量 · 演示数据" :
            projection?.Stale == true ? "上次电量" :
            projection?.Estimated == true ? "当前电量 · 估算" : "当前电量";
        var warning = projection is not null && (projection.Full || projection.RemainingSeconds is > 0 and <= 1800);
        var ink = Brush(warning ? "Warning" : "AccentText");
        Countdown.Foreground = MiniStatus.Foreground = ink;
        for (int i = 0; i < _segments.Length; i++)
        {
            var fill = Math.Clamp((projection?.Ratio ?? 0) * 24 - i, 0, 1);
            var accent = ((SolidColorBrush)Brush(projection?.Stale == true ? "Muted" : warning ? "Warning" : "Accent")).Color;
            var empty = ((SolidColorBrush)Brush("Stroke")).Color;
            _segments[i].Background = new LinearGradientBrush(new GradientStopCollection
            { new(accent, 0), new(accent, fill), new(empty, fill), new(empty, 1) }, 0);
        }
        Countdown.Text = projection is null ? "连接后查看实时电量" : EnergyEstimator.Countdown(projection);
        FinishTime.Text = "";
        if (snapshot is not null && projection is { Stale: false, Full: false, RemainingSeconds: not null } && snapshot.RestoreSeconds is > 0)
            FinishTime.Text = EnergyEstimator.FinishAt(snapshot.ReceivedAt.AddSeconds(snapshot.RestoreSeconds.Value), DateTimeOffset.UtcNow);
        if (projection is { Full: true, Stale: false }) FinishTime.Text = "自然恢复暂停";
        MiniStatus.Text = projection is null ? "尚未连接米游社" :
            projection.Stale ? "数据已过期 · 展开查看" :
            projection.Full ? "电量已满 · 恢复暂停" :
            projection.Inconsistent ? "等待同步确认恢复时间" : FinishTime.Text + " 回满";
        if (projection is { Stale: false } && session.Failure is not null)
            MiniStatus.Text = "离线估算 · " + MiniStatus.Text;
        if (app.Demo) { SyncText.Text = "演示数据 · 未连接账号"; MiniStatus.Text = "演示 · " + MiniStatus.Text; }
        else if (session.Busy) SyncText.Text = "正在同步…";
        else if (snapshot is null) SyncText.Text = session.Credential is null ? "尚未连接账号" : "等待首次同步";
        else SyncText.Text = (projection?.Stale == true ? "过期记录 · " : session.Failure is not null ? "离线估算 · " : projection?.Estimated == true ? "本地估算 · " : "") + snapshot.ReceivedAt.ToLocalTime().ToString("HH:mm") + " 同步";
        SyncText.ToolTip = session.Credential?.Role?.DisplayName + "\n" + session.Message;
        RefreshButton.IsEnabled = !session.Busy && session.Credential is not null && !app.Demo;
        var isVerificationIssue = session.Failure?.Kind == FailureKind.Verification ||
            (session.Failure?.Kind == FailureKind.Permission && session.Failure?.Code is 5003 or 10041);
        var needsConnection = session.Credential is null || session.Failure?.Kind == FailureKind.Login || isVerificationIssue;
        ConnectButton.Visibility = needsConnection || app.Demo ? Visibility.Visible : Visibility.Collapsed;
        ConnectButton.IsEnabled = !session.RecoveringInBackground;
        ConnectButton.Content = session.RecoveringInBackground ? "正在后台恢复…" : isVerificationIssue ? "恢复电量同步" :
            session.Failure?.Kind == FailureKind.Login ? "重新连接米游社" : "连接米游社";
        var notice = DateTimeOffset.UtcNow < _noticeUntil ? _notice :
            app.Demo ? "这是演示数据。连接自己的米游社账号后即可显示真实电量。" :
            session.RecoveringInBackground ? session.Message : session.Failure?.Message ?? (projection?.Stale == true ? "电量缓存已过期或时间发生变化，请刷新校准。" :
            projection?.Inconsistent == true ? "恢复时间与电量不一致，暂时保留服务端数值。" :
            snapshot is { SupplementMessage.Length: > 0 } ? snapshot.SupplementMessage : null);
        NoticePanel.Visibility = string.IsNullOrEmpty(notice) ? Visibility.Collapsed : Visibility.Visible;
        Notice.Text = notice ?? "";
        CharacterSync.Text = SyncText.Text;
        CharacterSync.ToolTip = SyncText.ToolTip;
        CharacterNotice.Text = Notice.Text;
        CharacterNoticePanel.Visibility = NoticePanel.Visibility;
        CharacterConnect.Visibility = ConnectButton.Visibility;
        CharacterConnect.Content = ConnectButton.Content;
        CharacterConnect.IsEnabled = ConnectButton.IsEnabled;
        CharacterNotesBorder.Visibility = app.Settings.Expanded && snapshot is not null ? Visibility.Visible : Visibility.Collapsed;
        var recovery = projection is null ? "连接后查看电量" : projection.Stale ? "数据过期 · 等待同步" :
            projection.Full ? (projection.Current > projection.Maximum ? "已超过自然恢复上限" : "电量已回满") :
            projection.Inconsistent ? "等待同步恢复时间" : FinishTime.Text + " 回满";
        Character.SetData(projection?.Current.ToString() ?? "—", projection?.Maximum.ToString() ?? "—", recovery, notes, RefreshButton.IsEnabled);
        DetailsButton.Visibility = snapshot is null ? Visibility.Collapsed : Visibility.Visible;
        NotesPanel.Visibility = app.Settings.Expanded && snapshot is not null ? Visibility.Visible : Visibility.Collapsed;
        DetailsButton.Content = app.Settings.Expanded ? "收起便笺  ⌃" : "今日便笺  ﹀";
        if (snapshot is not null && notes is not null)
        {
            var footer = $"来源：{snapshot.Source} · {snapshot.ReceivedAt.ToLocalTime():MM-dd HH:mm} 同步\n状态不会随电量估算更新。";
            if (!string.IsNullOrEmpty(snapshot.SupplementMessage)) footer += "\n" + snapshot.SupplementMessage;
            if (!notes.Any(n => n.Group == "活动")) footer += "\n本次接口未提供活动信息。";
            NotesPanel.SetNotes(notes, footer); CharacterNotes.SetNotes(notes, footer);
        }
    }
    private void DragWindow(object sender, MouseButtonEventArgs e)
    {
        if (App.Current.Settings.Locked || e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is DependencyObject source)
        {
            for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
                if (current is Button) return;
        }
        _placement.BeginUserMove();
        try { DragMove(); } catch (InvalidOperationException) { }
        finally { _placement.EndUserMove(); }
    }
    public void RestorePosition() => _placement.QueueRestore();
    public void FlushUserPosition() => _placement.FlushUserPosition();
    private async void RefreshClick(object sender, RoutedEventArgs e) => await App.Current.Refresh();
    private async void DailySignClick(object sender, RoutedEventArgs e) => await App.Current.SignToday();
    private void MiniClick(object sender, RoutedEventArgs e) { App.Current.Settings.Mini = !App.Current.Settings.Mini; App.Current.SaveSettings(); }
    private void DetailsClick(object sender, RoutedEventArgs e) { App.Current.Settings.Expanded = !App.Current.Settings.Expanded; App.Current.SaveSettings(); }
    private void AccountClick(object sender, RoutedEventArgs e)
    {
        var isVerificationIssue = App.Current.Session.Failure?.Kind == FailureKind.Verification ||
            (App.Current.Session.Failure?.Kind == FailureKind.Permission && App.Current.Session.Failure?.Code is 5003 or 10041);
        if (isVerificationIssue) App.Current.OpenVerification();
        else App.Current.OpenAccount();
    }
    private void SettingsClick(object sender, RoutedEventArgs e) => App.Current.OpenSettings();
    private void HideClick(object sender, RoutedEventArgs e) => Hide();
}
