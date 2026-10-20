using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using QRCoder;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

public partial class QrLoginWindow : Window
{
    public Credential? Credential { get; private set; }
    public IReadOnlyList<GameRole> Roles { get; private set; } = Array.Empty<GameRole>();
    private CancellationTokenSource? _active;
    private bool _closed;
    public QrLoginWindow() : this(false) { }
    internal QrLoginWindow(bool preview)
    {
        InitializeComponent();
        AdaptiveWindow.Attach(this, 320, 340);
        Loaded += async (_, _) =>
        {
            if (preview)
            {
                SetQrImage("https://user.mihoyo.com/#/login?demo=ZZZBattery");
                LoginStatus.Text = "界面检查示例 · 此码不能用于登录"; RefreshButton.IsEnabled = false;
            }
            else await RunLoginAsync();
        };
        Closed += (_, _) => { _closed = true; _active?.Cancel(); QrImage.Source = null; };
    }
    private void SetQrImage(string url)
    {
        using var data = QRCodeGenerator.GenerateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        using var bytes = new MemoryStream(qr.GetGraphic(8));
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = bytes; bitmap.EndInit(); bitmap.Freeze();
        QrImage.Source = bitmap; QrPlaceholder.Visibility = Visibility.Collapsed;
    }
    private async void RefreshClick(object sender, RoutedEventArgs e) => await RunLoginAsync();
    private void CancelClick(object sender, RoutedEventArgs e) => Close();
    private async Task RunLoginAsync()
    {
        _active?.Cancel();
        using var cancel = new CancellationTokenSource(); _active = cancel;
        var token = cancel.Token;
        Credential = null; Roles = Array.Empty<GameRole>();
        QrImage.Source = null; QrPlaceholder.Visibility = Visibility.Visible; QrPlaceholder.Text = "正在生成二维码…";
        RefreshButton.IsEnabled = false; LoginStatus.Text = "正在连接米哈游官方登录服务…";
        var stage = "生成二维码";
        try
        {
            var ticket = await App.Current.Client.CreateQrLoginAsync(token);
            token.ThrowIfCancellationRequested();
            SetQrImage(ticket.Url.AbsoluteUri); RefreshButton.IsEnabled = true;
            var scanned = false;
            while (true)
            {
                var left = Math.Max(0, (int)(ticket.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds);
                LoginStatus.Text = scanned ? "已扫描，请在手机米游社上确认登录。" : $"请用米游社 App 扫码 · 剩余 {left / 60}:{left % 60:00}";
                await Task.Delay(TimeSpan.FromSeconds(2), token);
                stage = "查询扫码确认";
                var result = await App.Current.Client.PollQrLoginAsync(ticket, token);
                token.ThrowIfCancellationRequested();
                if (result.State == QrLoginState.Expired)
                {
                    QrImage.Source = null; QrPlaceholder.Text = "二维码已过期"; QrPlaceholder.Visibility = Visibility.Visible;
                    LoginStatus.Text = "请点击“重新生成二维码”再扫码。"; return;
                }
                if (result.State == QrLoginState.Scanned)
                {
                    scanned = true;
                    LoginStatus.Text = "已扫描，请在手机米游社上确认登录。";
                }
                if (result.State != QrLoginState.Confirmed || result.Credential is null) continue;
                QrImage.Source = null; QrPlaceholder.Text = "扫码成功"; QrPlaceholder.Visibility = Visibility.Visible;
                RefreshButton.IsEnabled = false; LoginStatus.Text = "正在校验登录并读取绝区零角色…";
                stage = "转换便笺登录凭据";
                var credential = await App.Current.Client.CompleteQrCredentialAsync(result.Credential, token);
                stage = "读取游戏角色";
                var roles = await App.Current.Client.GetRolesAsync(credential, token);
                token.ThrowIfCancellationRequested();
                Credential = credential; Roles = roles; DialogResult = true; return;
            }
        }
        catch (OperationCanceledException) { }
        catch (ApiFailure error)
        {
            if (!token.IsCancellationRequested && !_closed)
            {
                QrImage.Source = null; QrPlaceholder.Text = "连接未完成"; QrPlaceholder.Visibility = Visibility.Visible;
                LoginStatus.Text = error.Kind == FailureKind.Verification
                    ? "官方要求进一步验证。请在米游社 App 内完成验证后重新扫码，或返回账号窗口使用网页登录。"
                    : $"{stage}未完成（{error.Code?.ToString() ?? error.Kind.ToString()}）。" +
                      (error.Kind == FailureKind.Login ? "登录服务未接受此次凭据，请重试；这不代表游戏账号本身已失效。" : error.Message);
                // Diagnostics contain stage/error only, never tickets, cookies or account IDs.
                try { File.WriteAllText(Path.Combine(App.Current.Store.Root, "login-diagnostic.json"),
                    JsonSerializer.Serialize(new { stage, kind = error.Kind.ToString(), code = error.Code, at = DateTimeOffset.UtcNow })); }
                catch (Exception writeError) when (writeError is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException)
        {
            if (!token.IsCancellationRequested && !_closed) LoginStatus.Text = "扫码连接未完成，请重新生成二维码后重试。";
        }
        finally
        {
            if (ReferenceEquals(_active, cancel))
            {
                _active = null;
                if (!_closed) RefreshButton.IsEnabled = true;
            }
        }
    }
}
