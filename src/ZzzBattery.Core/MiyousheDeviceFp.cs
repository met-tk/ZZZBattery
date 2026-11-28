using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZzzBattery.Core;

public sealed partial class MiyousheClient
{
    public const string DeviceFpEndpoint = "https://public-data-api.mihoyo.com/device-fp/api/getFp";
    private static readonly TimeSpan DeviceFpLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan DeviceFpRetryCooldown = TimeSpan.FromMinutes(10);

    private readonly SemaphoreSlim _fpGate = new(1, 1);
    private readonly object _fpStateGate = new();
    private string? _androidDeviceFp;
    private string? _webDeviceFp;
    private DateTimeOffset? _androidDeviceFpUpdatedAt;
    private DateTimeOffset? _webDeviceFpUpdatedAt;
    private DateTimeOffset _androidFpCooldownUntil;
    private DateTimeOffset _webFpCooldownUntil;

    public string? AndroidDeviceFp { get { lock (_fpStateGate) return _androidDeviceFp; } }
    public string? WebDeviceFp { get { lock (_fpStateGate) return _webDeviceFp; } }
    public DateTimeOffset? AndroidDeviceFpUpdatedAt { get { lock (_fpStateGate) return _androidDeviceFpUpdatedAt; } }
    public DateTimeOffset? WebDeviceFpUpdatedAt { get { lock (_fpStateGate) return _webDeviceFpUpdatedAt; } }
    public event Action<string?, string?, DateTimeOffset?, DateTimeOffset?>? DeviceFingerprintChanged;

    /// <summary>Restores persisted device fingerprints if they pass safe header validation.</summary>
    public void RestoreDeviceFingerprints(string? androidFp, string? webFp,
        DateTimeOffset? androidUpdatedAt, DateTimeOffset? webUpdatedAt = null)
    {
        webUpdatedAt ??= androidUpdatedAt;
        lock (_fpStateGate)
        {
            if (IsSafeVerificationValue(androidFp, 64)) _androidDeviceFp = androidFp;
            if (IsSafeVerificationValue(webFp, 64)) _webDeviceFp = webFp;
            if (androidUpdatedAt is { } a && a != default) _androidDeviceFpUpdatedAt = a;
            if (webUpdatedAt is { } w && w != default) _webDeviceFpUpdatedAt = w;
        }
    }

    private string? GetCachedDeviceFp(bool android)
    {
        lock (_fpStateGate) return android ? _androidDeviceFp : _webDeviceFp;
    }

    private (string? Fp, DateTimeOffset? UpdatedAt, DateTimeOffset CooldownUntil) ReadFpState(bool android)
    {
        lock (_fpStateGate)
        {
            return android
                ? (_androidDeviceFp, _androidDeviceFpUpdatedAt, _androidFpCooldownUntil)
                : (_webDeviceFp, _webDeviceFpUpdatedAt, _webFpCooldownUntil);
        }
    }

    /// <summary>Ensures a cached or newly registered device fingerprint for Android (platform=2) or Web (platform=4).</summary>
    public async Task<string?> EnsureDeviceFpAsync(bool android, CancellationToken cancellation = default)
    {
        var now = DateTimeOffset.UtcNow;
        var state = ReadFpState(android);
        if (IsFreshFp(state.Fp, state.UpdatedAt, now)) return state.Fp;
        if (now < state.CooldownUntil) return state.Fp;

        await _fpGate.WaitAsync(cancellation);
        try
        {
            now = DateTimeOffset.UtcNow;
            state = ReadFpState(android);
            if (IsFreshFp(state.Fp, state.UpdatedAt, now)) return state.Fp;
            if (now < state.CooldownUntil) return state.Fp;

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            var token = deadline.Token;

            var payload = BuildDeviceFpPayload(android, state.Fp);
            using var request = new HttpRequestMessage(HttpMethod.Post, DeviceFpEndpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.UserAgent.ParseAdd(android
                ? $"Mozilla/5.0 miHoYoBBS/{WidgetAppVersion}"
                : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0.0.0 Safari/537.36");

            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
            {
                SetFpCooldown(android, now + DeviceFpRetryCooldown);
                return state.Fp;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
            var root = doc.RootElement;
            if (JsonValue.Int(root, "retcode") != 0)
            {
                SetFpCooldown(android, now + DeviceFpRetryCooldown);
                return state.Fp;
            }

            var data = JsonValue.Object(root, "data");
            var code = JsonValue.Int(data, "code");
            var fp = JsonValue.String(data, "device_fp");
            if (code != 200 || !IsSafeVerificationValue(fp, 64))
            {
                SetFpCooldown(android, now + DeviceFpRetryCooldown);
                return state.Fp;
            }

            string? savedAndroidFp, savedWebFp;
            DateTimeOffset? savedAndroidAt, savedWebAt;
            lock (_fpStateGate)
            {
                if (android)
                {
                    _androidDeviceFp = fp;
                    _androidDeviceFpUpdatedAt = now;
                    _androidFpCooldownUntil = DateTimeOffset.MinValue;
                }
                else
                {
                    _webDeviceFp = fp;
                    _webDeviceFpUpdatedAt = now;
                    _webFpCooldownUntil = DateTimeOffset.MinValue;
                }
                savedAndroidFp = _androidDeviceFp;
                savedWebFp = _webDeviceFp;
                savedAndroidAt = _androidDeviceFpUpdatedAt;
                savedWebAt = _webDeviceFpUpdatedAt;
            }

            try { DeviceFingerprintChanged?.Invoke(savedAndroidFp, savedWebFp, savedAndroidAt, savedWebAt); }
            catch { /* Persistence failure must not fail a network request. */ }

            return fp;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            SetFpCooldown(android, DateTimeOffset.UtcNow + DeviceFpRetryCooldown);
            return ReadFpState(android).Fp;
        }
        finally
        {
            _fpGate.Release();
        }
    }

    private static bool IsFreshFp(string? fp, DateTimeOffset? updatedAt, DateTimeOffset now) =>
        !string.IsNullOrEmpty(fp) &&
        updatedAt is { } ts &&
        now >= ts &&
        now - ts < DeviceFpLifetime;

    private void SetFpCooldown(bool android, DateTimeOffset until)
    {
        lock (_fpStateGate)
        {
            if (android) _androidFpCooldownUntil = until;
            else _webFpCooldownUntil = until;
        }
    }

    private string BuildDeviceFpPayload(bool android, string? existingFp)
    {
        var stableDeviceId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(deviceId)))[..16].ToLowerInvariant();
        var seedId = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var seedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var fpSeed = string.IsNullOrEmpty(existingFp) ? "38d7f00000000" : existingFp;

        if (android)
        {
            var extFields = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["proxyStatus"] = 0,
                ["isRoot"] = 0,
                ["romCapacity"] = "512",
                ["deviceName"] = "Pixel 8",
                ["productName"] = "shiba",
                ["romRemain"] = "256",
                ["hostname"] = "android",
                ["screenSize"] = "1080x2400",
                ["isTablet"] = 0,
                ["aaId"] = "",
                ["model"] = "Pixel 8",
                ["brand"] = "google",
                ["hardware"] = "shiba",
                ["deviceType"] = "shiba",
                ["devId"] = "REL",
                ["serialNumber"] = "unknown",
                ["sdCapacity"] = "128000",
                ["buildTime"] = "1720000000000",
                ["buildUser"] = "android-build",
                ["simState"] = 5,
                ["ramRemain"] = "6144",
                ["appUpdateTimeDiff"] = 1720000000000L,
                ["deviceInfo"] = "google/shiba/shiba:14/AP2A.240805.005/12025142:user/release-keys",
                ["vaId"] = "",
                ["buildType"] = "user",
                ["sdkVersion"] = "34",
                ["ui_mode"] = "UI_MODE_TYPE_NORMAL",
                ["isMockLocation"] = 0,
                ["cpuType"] = "arm64-v8a",
                ["isAirMode"] = 0,
                ["ringMode"] = 2,
                ["app_set_id"] = deviceId,
                ["app_set_id_scope"] = 1,
                ["app_set_id_v2"] = deviceId,
                ["chargeStatus"] = 1,
                ["manufacturer"] = "Google",
                ["emulatorStatus"] = 0,
                ["appMemory"] = "512",
                ["osVersion"] = "14",
                ["vendor"] = "unknown",
                ["accelerometer"] = "0.0x9.8x0.0",
                ["sdRemain"] = "64000",
                ["buildTags"] = "release-keys",
                ["packageName"] = "com.mihoyo.hyperion",
                ["networkType"] = "WiFi",
                ["oaid"] = "",
                ["debugStatus"] = 1,
                ["ramCapacity"] = "12288",
                ["magnetometer"] = "0.0x0.0x0.0",
                ["display"] = "AP2A.240805.005",
                ["appInstallTimeDiff"] = 1720000000000L,
                ["packageVersion"] = WidgetAppVersion,
                ["gyroscope"] = "0.0x0.0x0.0",
                ["batteryStatus"] = 80,
                ["hasKeyboard"] = 0,
                ["board"] = "shiba"
            });

            return JsonSerializer.Serialize(new
            {
                device_id = stableDeviceId,
                seed_id = seedId,
                seed_time = seedTime,
                platform = "2",
                device_fp = fpSeed,
                app_name = "bbs_cn",
                ext_fields = extFields,
                bbs_device_id = deviceId
            });
        }
        else
        {
            var extFields = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["userAgent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0.0.0 Safari/537.36",
                ["browserScreenSize"] = "1920x1080",
                ["maxTouchPoints"] = 0,
                ["isTouchSupported"] = false,
                ["browserLanguage"] = "zh-CN",
                ["browserPlat"] = "Win32",
                ["browserTimeZone"] = "Asia/Shanghai",
                ["webGlRender"] = "ANGLE (Intel)",
                ["webGlVendor"] = "Google Inc.",
                ["numOfPlugins"] = 5,
                ["listOfPlugins"] = new[] { "PDF Viewer" },
                ["screenRatio"] = 1,
                ["deviceMemory"] = "8",
                ["hardwareConcurrency"] = "8",
                ["cpuClass"] = "unknown",
                ["ifNotTrack"] = "unknown",
                ["ifAdBlock"] = 0,
                ["hasLiedLanguage"] = 1,
                ["hasLiedResolution"] = 1,
                ["hasLiedOs"] = 0,
                ["hasLiedBrowser"] = 0,
                ["canvas"] = "abc",
                ["webDriver"] = 0,
                ["colorDepth"] = "24",
                ["pixelRatio"] = "1",
                ["packageName"] = "unknown",
                ["packageVersion"] = "2.29.0",
                ["webrtc"] = ""
            });

            return JsonSerializer.Serialize(new
            {
                device_id = stableDeviceId,
                seed_id = seedId,
                seed_time = seedTime,
                platform = "4",
                device_fp = fpSeed,
                app_name = "bbs_cn",
                ext_fields = extFields
            });
        }
    }
}
