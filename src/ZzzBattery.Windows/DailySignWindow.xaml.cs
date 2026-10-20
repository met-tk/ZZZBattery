using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QRCoder;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

public partial class DailySignWindow : Window
{
    internal const string OfficialUrl = MiyousheClient.DailySignPage;
    private readonly string _accountKey;
    private readonly string? _roleKey;
    private readonly DailySignSession _session;
    private readonly DispatcherTimer _presentationTimer;
    private bool _closed;
    private string? _localMessage;

    public DailySignWindow(Credential credential)
    {
        // Keep identity only. The public QR code and external browser receive no credentials.
        _accountKey = credential.AccountKey;
        _roleKey = credential.Role?.Key;
        _session = App.Current.DailySign;
        InitializeComponent();
        AdaptiveWindow.Attach(this, 300, 340);
        SetQrImage();
        _session.Changed += SessionChanged;
        _presentationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _presentationTimer.Tick += (_, _) => Render();
        Loaded += (_, _) => { Render(); _presentationTimer.Start(); };
        Closed += (_, _) =>
        {
            _closed = true;
            _presentationTimer.Stop();
            _session.Changed -= SessionChanged;
            QrImage.Source = null;
        };
    }

    private bool SameAccount() => _roleKey is not null &&
        App.Current.Session.Credential is { } current &&
        current.AccountKey == _accountKey && current.Role?.Key == _roleKey &&
        _session.Credential?.AccountKey == _accountKey && _session.Credential.Role?.Key == _roleKey;

    private void SetQrImage()
    {
        using var data = QRCodeGenerator.GenerateQrCode(OfficialUrl, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        using var bytes = new MemoryStream(qr.GetGraphic(8));
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = bytes;
        bitmap.EndInit();
        bitmap.Freeze();
        QrImage.Source = bitmap;
    }

    private void SessionChanged()
    {
        if (_closed || Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (_closed) return;
            _localMessage = null;
            Render();
        });
    }

    private void Render()
    {
        if (_closed) return;
        var sameAccount = SameAccount();
        BrowserButton.IsEnabled = sameAccount;
        if (!sameAccount)
        {
            StatusHeading.Text = "账号或角色已切换";
            StatusText.Text = "请关闭此窗口，从当前角色的小组件重新打开签到。";
            CheckButton.Content = "请重新打开签到";
            CheckButton.IsEnabled = false;
            return;
        }
        if (_session.IsSignedToday)
        {
            StatusHeading.Text = "今日已签到";
            StatusText.Text = "已确认当前角色今日的签到状态，小组件按钮已更新。";
            CheckButton.Content = "已签到";
            CheckButton.IsEnabled = false;
            return;
        }

        StatusHeading.Text = _session.Busy ? "正在确认签到状态" :
            _session.Failure?.Kind == FailureKind.Verification ? "需要在官方页面继续" : "在官方页面完成签到";
        StatusText.Text = _localMessage ?? _session.Message;
        var seconds = Math.Max(0, (int)Math.Ceiling((_session.RetryAt - DateTimeOffset.UtcNow).TotalSeconds));
        CheckButton.Content = _session.Busy ? "正在检查…" : seconds > 0
            ? $"稍后可检查 · {seconds / 60:00}:{seconds % 60:00}" : "已完成，检查签到状态";
        CheckButton.IsEnabled = !_session.Busy && seconds == 0;
    }

    private async void CheckClick(object sender, RoutedEventArgs e)
    {
        if (!SameAccount() || _session.Busy || _session.IsSignedToday) { Render(); return; }
        _localMessage = null;
        await App.Current.CheckDailySign();
        if (!_closed) Render();
    }

    private void BrowserClick(object sender, RoutedEventArgs e)
    {
        if (!SameAccount()) { Render(); return; }
        try { Process.Start(new ProcessStartInfo(OfficialUrl) { UseShellExecute = true }); }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            _localMessage = "未能打开系统浏览器，请用米游社 App 扫描上方二维码。";
            Render();
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
