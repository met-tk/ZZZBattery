using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

internal static class OfficialNavigationPolicy
{
    private static bool IsGeetestHost(string host) =>
        host.Equals("geetest.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".geetest.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("geevisit.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".geevisit.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("geetest.cn", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".geetest.cn", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("geecdn.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".geecdn.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsOfficialVendorHost(string host) =>
        host.EndsWith(".volces.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".byteoversea.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".qbox.me", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("hoyoverse.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".hoyoverse.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".bilibili.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".hdslb.com", StringComparison.OrdinalIgnoreCase);

    public static bool IsAllowed(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0) return false;

        var host = uri.Host;
        return Cookies.IsOfficialHost(host) || IsGeetestHost(host) || IsOfficialVendorHost(host);
    }
}

// WebView profiles persist local storage, but session cookies disappear when Windows exits.
// Keep their original scope/expiry in the same user-protected store as the login credential.
internal static class OfficialBrowserSession
{
    internal sealed record SavedCookie(string Name, string Value, string Domain, string Path,
        DateTime? Expires, bool Secure, bool HttpOnly, CoreWebView2CookieSameSiteKind SameSite);
    internal sealed record SavedSession(string AccountKey, string CredentialCookie, SavedCookie[] Cookies);
    private const string FileName = "browser-session.dpapi";
    private static readonly SemaphoreSlim SessionGate = new(1, 1);
    private static readonly object FileLock = new();
    private static long _sessionEpoch;

    private static readonly HashSet<string> IdentityCookieNames = new(StringComparer.Ordinal)
    {
        "account_id", "account_id_v2", "ltuid", "ltuid_v2", "stuid", "stuid_v2"
    };

    internal static string StripLeadingDot(string domain) =>
        domain.StartsWith('.') ? domain[1..] : domain;

    private static bool IsForeignOrMixedCookieSet(IEnumerable<CoreWebView2Cookie> official, string accountKey)
    {
        foreach (var cookie in official)
        {
            if (!IdentityCookieNames.Contains(cookie.Name) || string.IsNullOrEmpty(cookie.Value) ||
                !cookie.Value.All(char.IsAsciiDigit))
                continue;
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cookie.Value)))[..24];
            if (!string.Equals(key, accountKey, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    internal static async Task<SavedCookie[]?> CaptureOfficialCookiesAsync(CoreWebView2 core, Credential credential, CancellationToken token)
    {
        await SessionGate.WaitAsync(token);
        try
        {
            var all = await core.CookieManager.GetCookiesAsync(null).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            var official = all.Where(c => Cookies.IsOfficialHost(StripLeadingDot(c.Domain)) &&
                (c.IsSession || c.Expires > DateTime.UtcNow)).ToArray();
            if (official.Length == 0 || official.Length > 1000) return null;
            Credential browser;
            try { browser = Cookies.CreateFromBrowserCookies(official.Select(c => (c.Name, c.Value))); }
            catch (ArgumentException) { return null; }
            if (browser.AccountKey != credential.AccountKey) return null;
            return official.Select(c => new SavedCookie(c.Name, c.Value, c.Domain, c.Path,
                c.IsSession ? null : c.Expires, c.IsSecure, c.IsHttpOnly, c.SameSite)).ToArray();
        }
        finally
        {
            SessionGate.Release();
        }
    }

    internal static void UseCredential(Credential credential, LocalStore store, SavedCookie[]? initialCookies = null, bool preserveSameAccountSession = false)
    {
        Interlocked.Increment(ref _sessionEpoch);
        lock (FileLock)
        {
            var cookiesToSave = initialCookies;
            if (cookiesToSave is null && preserveSameAccountSession)
            {
                try
                {
                    var existing = store.ReadProtected<SavedSession>(FileName);
                    if (existing is not null && existing.AccountKey == credential.AccountKey && existing.Cookies is { Length: > 0 })
                        cookiesToSave = existing.Cookies.Where(c => !Cookies.Allowed.Contains(c.Name)).ToArray();
                }
                catch (Exception error) when (error is IOException or CryptographicException or JsonException) { }
            }
            store.WriteProtected(FileName, new SavedSession(credential.AccountKey, "", cookiesToSave ?? []));
        }
    }

    internal static async Task PrepareAsync(CoreWebView2 core, Credential credential, LocalStore store, CancellationToken token, bool allowReplace = true)
    {
        await SessionGate.WaitAsync(token);
        try
        {
            var epoch = Volatile.Read(ref _sessionEpoch);
            var existing = await core.CookieManager.GetCookiesAsync(null).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            var keys = existing.Select(c => (c.Name, c.Domain, c.Path)).ToHashSet();
            SavedSession? saved;
            lock (FileLock) saved = store.ReadProtected<SavedSession>(FileName);

            var officialExisting = existing.Where(c => Cookies.IsOfficialHost(StripLeadingDot(c.Domain))).ToArray();
            var hasForeignCookies = allowReplace && (
                (saved is not null && saved.AccountKey != credential.AccountKey) ||
                IsForeignOrMixedCookieSet(officialExisting, credential.AccountKey));

            var incoming = Cookies.Parse(credential.Cookie);
            var sameSavedAccount = saved?.AccountKey == credential.AccountKey;
            var previous = allowReplace && sameSavedAccount
                ? Cookies.Parse(saved!.CredentialCookie)
                : new Dictionary<string, string>(StringComparer.Ordinal);
            var explicitLogin = allowReplace && sameSavedAccount && saved!.CredentialCookie == "";
            var changed = allowReplace && sameSavedAccount
                ? incoming.Where(p => previous.GetValueOrDefault(p.Key) != p.Value).Select(p => p.Key).ToHashSet()
                : new HashSet<string>(StringComparer.Ordinal);
            var removed = allowReplace && sameSavedAccount
                ? previous.Keys.Where(k => !incoming.ContainsKey(k) && Cookies.Allowed.Contains(k)).ToHashSet()
                : new HashSet<string>(StringComparer.Ordinal);

            bool ShouldDeleteExisting(string name) =>
                (hasForeignCookies && Cookies.Allowed.Contains(name)) ||
                changed.Contains(name) ||
                removed.Contains(name) ||
                (explicitLogin && Cookies.Allowed.Contains(name));

            if (hasForeignCookies || explicitLogin || changed.Count > 0 || removed.Count > 0)
            {
                foreach (var cookie in officialExisting.Where(c => ShouldDeleteExisting(c.Name)))
                {
                    core.CookieManager.DeleteCookie(cookie);
                    keys.Remove((cookie.Name, cookie.Domain, cookie.Path));
                }
            }

            if (sameSavedAccount && saved!.Cookies is { Length: <= 1000 })
            {
                foreach (var value in saved.Cookies)
                {
                    token.ThrowIfCancellationRequested();
                    if (!Cookies.IsOfficialHost(StripLeadingDot(value.Domain)) ||
                        changed.Contains(value.Name) ||
                        removed.Contains(value.Name) ||
                        value.Expires is { } expiry && expiry <= DateTime.UtcNow ||
                        !keys.Add((value.Name, value.Domain, value.Path))) continue;
                    try
                    {
                        var cookie = core.CookieManager.CreateCookie(value.Name, value.Value, value.Domain, value.Path);
                        if (value.Expires is { } expires) cookie.Expires = expires;
                        cookie.IsSecure = value.Secure;
                        cookie.IsHttpOnly = value.HttpOnly;
                        cookie.SameSite = value.SameSite;
                        core.CookieManager.AddOrUpdateCookie(cookie);
                    }
                    catch (ArgumentException) { }
                }
            }

            // Only seed missing cookies: opening a window must not overwrite a newer browser login.
            foreach (var (name, value) in incoming)
                foreach (var domain in new[] { ".mihoyo.com", ".miyoushe.com" })
                {
                    if (!keys.Add((name, domain, "/"))) continue;
                    try
                    {
                        var cookie = core.CookieManager.CreateCookie(name, value, domain, "/");
                        cookie.IsSecure = true;
                        core.CookieManager.AddOrUpdateCookie(cookie);
                    }
                    catch (ArgumentException) { }
                }

            // Consume explicitLogin once so subsequent retries/windows do not wipe freshly earned browser cookies.
            if (explicitLogin && Volatile.Read(ref _sessionEpoch) == epoch)
            {
                lock (FileLock)
                {
                    if (Volatile.Read(ref _sessionEpoch) == epoch)
                        store.WriteProtected(FileName, new SavedSession(credential.AccountKey, credential.Cookie, saved!.Cookies ?? []));
                }
            }
        }
        finally
        {
            SessionGate.Release();
        }
    }

    internal static async Task<bool> SaveAsync(CoreWebView2 core, Credential credential, LocalStore store, CancellationToken token)
    {
        await SessionGate.WaitAsync(token);
        try
        {
            var epoch = Volatile.Read(ref _sessionEpoch);
            var all = await core.CookieManager.GetCookiesAsync(null).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            var official = all.Where(c => Cookies.IsOfficialHost(StripLeadingDot(c.Domain)) &&
                (c.IsSession || c.Expires > DateTime.UtcNow)).ToArray();
            if (official.Length > 1000) return false;
            // Validate every identity before selecting the cookies relevant to the note API.
            Credential browser;
            try { browser = Cookies.CreateFromBrowserCookies(official.Select(c => (c.Name, c.Value))); }
            catch (ArgumentException) { return false; }
            if (browser.AccountKey != credential.AccountKey) return false;
            var effective = await core.CookieManager.GetCookiesAsync(MiyousheClient.NoteEndpoint).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            if (Volatile.Read(ref _sessionEpoch) != epoch) return false;
            var before = credential.Cookie;
            if (!Cookies.TryMergeBrowserCookies(credential, effective.OrderBy(c => c.Domain.Length).Select(c => (c.Name, c.Value))))
                return false;
            store.SaveCredential(credential);

            var mergedMap = Cookies.Parse(credential.Cookie);
            var effectiveMap = effective
                .OrderBy(c => c.Domain.Length)
                .Where(c => Cookies.Allowed.Contains(c.Name) && !string.IsNullOrWhiteSpace(c.Value))
                .GroupBy(c => c.Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);
            var savedCookies = official.Select(c =>
            {
                var value = c.Value;
                if (Cookies.Allowed.Contains(c.Name) && mergedMap.TryGetValue(c.Name, out var latest))
                    value = latest;
                else if (effectiveMap.TryGetValue(c.Name, out var effVal))
                    value = effVal;
                return new SavedCookie(c.Name, value, c.Domain, c.Path, c.IsSession ? null : c.Expires,
                    c.IsSecure, c.IsHttpOnly, c.SameSite);
            }).ToArray();

            lock (FileLock)
            {
                if (Volatile.Read(ref _sessionEpoch) != epoch) return false;
                store.WriteProtected(FileName, new SavedSession(credential.AccountKey, credential.Cookie, savedCookies));
            }
            return before != credential.Cookie;
        }
        finally
        {
            SessionGate.Release();
        }
    }
}
